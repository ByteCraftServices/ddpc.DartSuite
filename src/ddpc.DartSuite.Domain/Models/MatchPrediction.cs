namespace ddpc.DartSuite.Domain.Models;

public sealed record MatchPrediction(
    double HomeWinProbability,
    double AwayWinProbability,
    TimeSpan EstimatedRemainingDuration,
    string ExpectedResult,
    string ExpectedFinalScore,
    double ExpectedCheckoutPoints,
    double CheckoutProbability,
    double ExpectedRemainingDartsInLeg,
    double ExpectedPpr);