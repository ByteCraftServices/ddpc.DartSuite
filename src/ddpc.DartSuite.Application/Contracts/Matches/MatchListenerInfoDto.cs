namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record MatchListenerInfoDto(
    Guid MatchId,
    string ExternalMatchId,
    Guid? BoardId,
    bool IsRunning,
    DateTimeOffset? LastUpdateUtc,
    string? LastError,
    bool IsWebSocketActive,
    string TransportMode,
    bool IsFallbackActive,
    DateTimeOffset? LastRealtimeEventUtc);
