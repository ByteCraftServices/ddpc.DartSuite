namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record LegResultRequest(
    Guid TournamentId,
    Guid MatchId,
    int LegNumber,
    string Player1Name,
    int Player1Sets,
    int Player1Legs,
    double Player1Average,
    string Player2Name,
    int Player2Sets,
    int Player2Legs,
    double Player2Average,
    DateTimeOffset StartTime,
    DateTimeOffset GameShotTime,
    int MatchDurationSeconds);
