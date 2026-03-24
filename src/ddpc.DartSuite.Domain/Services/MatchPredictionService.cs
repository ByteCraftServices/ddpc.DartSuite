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
        var playedLegs = Math.Max(1, homeLegs + awayLegs);
        var averageLegDuration = TimeSpan.FromSeconds(elapsed.TotalSeconds / playedLegs);
        var remainingLegs = Math.Max(1, targetLegs - Math.Max(homeLegs, awayLegs));
        var scoreMomentum = Math.Clamp((awayRemainingScore - homeRemainingScore) / 501d, -1, 1);
        var homeWinProbability = Math.Clamp(0.5 + scoreMomentum * 0.35, 0.05, 0.95);
        var awayWinProbability = 1 - homeWinProbability;
        var estimatedRemaining = TimeSpan.FromSeconds(averageLegDuration.TotalSeconds * remainingLegs);
        var expectedResult = homeWinProbability >= awayWinProbability ? "Home wins" : "Away wins";

        return new MatchPrediction(homeWinProbability, awayWinProbability, estimatedRemaining, expectedResult);
    }
}