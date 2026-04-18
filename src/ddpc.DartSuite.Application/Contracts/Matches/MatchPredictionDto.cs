namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record MatchPredictionDto(
    double HomeWinProbability,
    double AwayWinProbability,
    int EstimatedRemainingMinutes,
    string ExpectedResult,
    string ExpectedFinalScore,
    double ExpectedCheckoutPoints,
    double CheckoutProbability,
    double ExpectedRemainingDartsInLeg,
    double ExpectedPpr);