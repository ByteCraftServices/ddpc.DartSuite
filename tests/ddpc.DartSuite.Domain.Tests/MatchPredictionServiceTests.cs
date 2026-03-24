using ddpc.DartSuite.Domain.Services;
using FluentAssertions;

namespace ddpc.DartSuite.Domain.Tests;

public sealed class MatchPredictionServiceTests
{
    [Fact]
    public void Predict_ShouldReturnValidProbabilities()
    {
        var service = new MatchPredictionService();

        var result = service.Predict(3, 1, 1, 120, 240, TimeSpan.FromMinutes(10));

        result.HomeWinProbability.Should().BeGreaterThan(0);
        result.AwayWinProbability.Should().BeGreaterThan(0);
        (result.HomeWinProbability + result.AwayWinProbability).Should().BeApproximately(1, 0.0001);
    }
}