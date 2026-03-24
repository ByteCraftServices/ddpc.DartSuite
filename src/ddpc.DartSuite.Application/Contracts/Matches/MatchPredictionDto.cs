namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record MatchPredictionDto(
    double HomeWinProbability,
    double AwayWinProbability,
    int EstimatedRemainingMinutes,
    string ExpectedResult);