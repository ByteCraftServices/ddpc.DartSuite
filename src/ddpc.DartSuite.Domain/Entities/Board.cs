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
    public Guid? CurrentMatchId { get; set; }
    public string? CurrentMatchLabel { get; set; }
    public BoardManagedMode ManagedMode { get; set; } = BoardManagedMode.Manual;
    public Guid? TournamentId { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastExtensionPollUtc { get; set; }
}