namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record PageEventRequest(
    string? SourceUrl,
    string? MatchId,
    string? LobbyId);
