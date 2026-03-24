namespace ddpc.DartSuite.Domain.Models;

public sealed record MatchPrediction(
    double HomeWinProbability,
    double AwayWinProbability,
    TimeSpan EstimatedRemainingDuration,
    string ExpectedResult);