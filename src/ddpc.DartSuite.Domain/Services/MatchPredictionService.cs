using ddpc.DartSuite.Domain.Models;

namespace ddpc.DartSuite.Domain.Services;

public sealed class MatchPredictionService : IMatchPredictionService
{
    public MatchPrediction Predict(
        int targetLegs,
        int homeLegs,
        int awayLegs,
        int homeRemainingScore,
        int awayRemainingScore,
        TimeSpan elapsed)
    {
        targetLegs = Math.Max(1, targetLegs);
        homeLegs = Math.Max(0, homeLegs);
        awayLegs = Math.Max(0, awayLegs);
        homeRemainingScore = Math.Max(0, homeRemainingScore);
        awayRemainingScore = Math.Max(0, awayRemainingScore);

        var playedLegs = Math.Max(1, homeLegs + awayLegs);
        var averageLegDuration = TimeSpan.FromSeconds(elapsed.TotalSeconds / playedLegs);
        var remainingLegs = Math.Max(1, targetLegs - Math.Max(homeLegs, awayLegs));
        var scoreMomentum = Math.Clamp((awayRemainingScore - homeRemainingScore) / 501d, -1, 1);
        var homeWinProbability = Math.Clamp(0.5 + scoreMomentum * 0.35, 0.05, 0.95);
        var awayWinProbability = 1 - homeWinProbability;
        var estimatedRemaining = TimeSpan.FromSeconds(averageLegDuration.TotalSeconds * remainingLegs);
        var expectedResult = homeWinProbability >= awayWinProbability ? "Home wins" : "Away wins";

        var expectedHomeLegs = homeWinProbability >= awayWinProbability
            ? targetLegs
            : Math.Min(targetLegs - 1, homeLegs + remainingLegs - 1);
        var expectedAwayLegs = homeWinProbability >= awayWinProbability
            ? Math.Min(targetLegs - 1, awayLegs + remainingLegs - 1)
            : targetLegs;
        var expectedFinalScore = $"{Math.Max(0, expectedHomeLegs)}:{Math.Max(0, expectedAwayLegs)}";

        var minRemaining = Math.Min(homeRemainingScore, awayRemainingScore);
        var maxRemaining = Math.Max(homeRemainingScore, awayRemainingScore);
        var expectedCheckoutPoints = Math.Clamp((minRemaining * 0.72) + (maxRemaining * 0.08), 2d, 170d);
        var checkoutProbability = minRemaining switch
        {
            <= 40 => 0.84,
            <= 80 => 0.68,
            <= 120 => 0.52,
            <= 170 => 0.39,
            _ => 0.22
        };
        checkoutProbability = Math.Clamp(checkoutProbability + ((homeWinProbability - 0.5) * 0.15), 0.05, 0.95);

        var expectedPpr = Math.Clamp(66d + ((homeWinProbability - awayWinProbability) * 14d), 45d, 105d);
        var expectedRemainingDartsInLeg = Math.Clamp((Math.Max(1d, minRemaining) / Math.Max(35d, expectedPpr)) * 3d, 1d, 24d);

        return new MatchPrediction(
            homeWinProbability,
            awayWinProbability,
            estimatedRemaining,
            expectedResult,
            expectedFinalScore,
            Math.Round(expectedCheckoutPoints, 2),
            Math.Round(checkoutProbability, 4),
            Math.Round(expectedRemainingDartsInLeg, 2),
            Math.Round(expectedPpr, 2));
    }
}