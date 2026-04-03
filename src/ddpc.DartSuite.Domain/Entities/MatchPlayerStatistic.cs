namespace ddpc.DartSuite.Domain.Entities;

/// <summary>Per-player statistics for a completed match, sourced from autodarts.io API.</summary>
public sealed class MatchPlayerStatistic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public Guid ParticipantId { get; set; }

    public double Average { get; set; }
    public double First9Average { get; set; }
    public int DartsThrown { get; set; }
    public int LegsWon { get; set; }
    public int LegsLost { get; set; }
    public int SetsWon { get; set; }
    public int SetsLost { get; set; }
    public int HighestCheckout { get; set; }
    public double CheckoutPercent { get; set; }
    public int CheckoutHits { get; set; }
    public int CheckoutAttempts { get; set; }
    public int Plus100 { get; set; }
    public int Plus140 { get; set; }
    public int Plus170 { get; set; }
    public int Plus180 { get; set; }
    public int Breaks { get; set; }
    public double AverageDartsPerLeg { get; set; }
    public int BestLegDarts { get; set; }
    public int WorstLegDarts { get; set; }
    public int TonPlusCheckouts { get; set; }
    public double DoubleQuota { get; set; }
    public int TotalPoints { get; set; }
    public int? HighestRoundScore { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
