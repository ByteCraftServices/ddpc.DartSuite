using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

public sealed class Board
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ExternalBoardId { get; set; }
    public required string Name { get; set; }
    public string? LocalIpAddress { get; set; }
    public string? BoardManagerUrl { get; set; }
    public BoardStatus Status { get; set; } = BoardStatus.Offline;
    public ConnectionState ConnectionState { get; set; } = ConnectionState.Offline;
    public ExtensionConnectionStatus ExtensionStatus { get; set; } = ExtensionConnectionStatus.Offline;
    public SchedulingStatus SchedulingStatus { get; set; } = SchedulingStatus.None;
    public Guid? CurrentMatchId { get; set; }
    public string? CurrentMatchLabel { get; set; }
    public BoardManagedMode ManagedMode { get; set; } = BoardManagedMode.Manual;
    public Guid? TournamentId { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastExtensionPollUtc { get; set; }

    /// <summary>When true, this is a virtual board with no physical hardware.</summary>
    public bool IsVirtual { get; set; }

    /// <summary>The Autodarts account name of the owner for virtual boards.</summary>
    public string? OwnerAccountName { get; set; }

    /// <summary>Computes the overall board status based on individual status fields.</summary>
    public OverallBoardStatus ComputeOverallStatus()
    {
        // Virtual boards are always treated as ready/online
        if (IsVirtual)
            return OverallBoardStatus.Ok;

        bool hasActiveMatch = CurrentMatchId is not null;

        // Error conditions
        if (Status == BoardStatus.Error || Status == BoardStatus.Offline)
            return OverallBoardStatus.Error;
        if (ConnectionState == ConnectionState.Offline)
            return OverallBoardStatus.Error;
        if (SchedulingStatus == SchedulingStatus.Delayed)
            return OverallBoardStatus.Error;

        // Warning conditions
        if (Status == BoardStatus.Starting || Status == BoardStatus.Online)
            return OverallBoardStatus.Warning;
        if (SchedulingStatus == SchedulingStatus.Ahead)
            return OverallBoardStatus.Warning;
        if (hasActiveMatch && ExtensionStatus != ExtensionConnectionStatus.Listening)
            return OverallBoardStatus.Warning;

        // OK: Running + Online + Connected, and Listening if match active
        if (Status == BoardStatus.Running && ConnectionState == ConnectionState.Online)
        {
            if (!hasActiveMatch || ExtensionStatus == ExtensionConnectionStatus.Listening)
                return OverallBoardStatus.Ok;
        }

        return OverallBoardStatus.Warning;
    }
}