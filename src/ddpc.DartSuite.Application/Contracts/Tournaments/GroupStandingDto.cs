namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record GroupStandingDto(
    Guid ParticipantId,
    string ParticipantName,
    int GroupNumber,
    int Played,
    int Won,
    int Lost,
    int Points,
    int LegsWon,
    int LegsLost,
    int LegDifference,
    double Average = 0,
    double HighestAverage = 0,
    int HighestCheckout = 0,
    double AverageDartsPerLeg = 0,
    double CheckoutPercent = 0,
    int Breaks = 0,
    int Rank = 0,
    string? TiebreakerApplied = null);
