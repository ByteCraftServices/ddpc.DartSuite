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
    int LegDifference);
