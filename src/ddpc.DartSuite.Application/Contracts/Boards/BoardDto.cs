namespace ddpc.DartSuite.Application.Contracts.Boards;

public sealed record BoardDto(
    Guid Id,
    string ExternalBoardId,
    string Name,
    string Status,
    string ConnectionState,
    string ExtensionStatus,
    string SchedulingStatus,
    string OverallStatus,
    string? LocalIpAddress,
    string? BoardManagerUrl,
    Guid? CurrentMatchId,
    string? CurrentMatchLabel,
    string ManagedMode,
    Guid? TournamentId,
    DateTimeOffset UpdatedUtc,
    bool IsExtensionConnected,
    bool IsVirtual = false,
    string? OwnerAccountName = null);