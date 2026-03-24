using ddpc.DartSuite.Domain.Models;

namespace ddpc.DartSuite.Domain.Services;

public interface IMatchPredictionService
{
    MatchPrediction Predict(
        int targetLegs,
        int homeLegs,
        int awayLegs,
        int homeRemainingScore,
        int awayRemainingScore,
        TimeSpan elapsed);
}