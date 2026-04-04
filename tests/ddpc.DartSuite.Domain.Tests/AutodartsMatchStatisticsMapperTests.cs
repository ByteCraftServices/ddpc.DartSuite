using System.Text.Json;
using ddpc.DartSuite.Domain.Services;
using FluentAssertions;

namespace ddpc.DartSuite.Domain.Tests;

public sealed class AutodartsMatchStatisticsMapperTests
{
    [Fact]
    public void Map_NoStatsArray_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("{\"id\":\"m1\"}");

        var result = AutodartsMatchStatisticsMapper.Map(doc.RootElement);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Map_MatchStatsArray_MapsCoreFields()
    {
        const string json = """
        {
          "stats": [
            {
              "matchStats": {
                "dartsThrown": 11,
                "average": 43.36,
                "first9Average": 41.11,
                "checkouts": 2,
                "checkoutPoints": 48,
                "plus100": 1,
                "plus140": 0,
                "plus170": 0,
                "total180": 1,
                "score": 159,
                "checkoutsHit": 1,
                "checkoutPercent": 0.5,
                "legsWon": 2,
                "legsLost": 1
              }
            },
            {
              "matchStats": {
                "dartsThrown": 15,
                "average": 52.12,
                "first9Average": 50.01,
                "checkouts": 3,
                "checkoutPoints": 76,
                "plus100": 2,
                "plus140": 1,
                "plus170": 0,
                "total180": 0,
                "score": 221,
                "checkoutsHit": 2,
                "checkoutPercent": 0.66,
                "legsWon": 1,
                "legsLost": 2
              }
            }
          ]
        }
        """;

        using var doc = JsonDocument.Parse(json);

        var result = AutodartsMatchStatisticsMapper.Map(doc.RootElement);

        result.Should().HaveCount(2);

        var home = result[0];
        home.Slot.Should().Be(0);
        home.Average.Should().Be(43.36);
        home.First9Average.Should().Be(41.11);
        home.DartsThrown.Should().Be(11);
        home.CheckoutAttempts.Should().Be(2);
        home.CheckoutHits.Should().Be(1);
        home.CheckoutPercent.Should().Be(0.5);
        home.HighestCheckout.Should().Be(48);
        home.Plus100.Should().Be(1);
        home.Plus180.Should().Be(1);
        home.TotalPoints.Should().Be(159);
        home.LegsWon.Should().Be(2);
        home.LegsLost.Should().Be(1);
        home.AverageDartsPerLeg.Should().BeApproximately(3.67, 0.01);

        var away = result[1];
        away.Slot.Should().Be(1);
        away.Average.Should().Be(52.12);
        away.CheckoutAttempts.Should().Be(3);
        away.CheckoutHits.Should().Be(2);
        away.Plus140.Should().Be(1);
        away.TotalPoints.Should().Be(221);
    }
}
