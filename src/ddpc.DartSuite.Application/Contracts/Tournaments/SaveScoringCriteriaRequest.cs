namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record SaveScoringCriteriaRequest(
    Guid TournamentId,
    IReadOnlyList<ScoringCriterionDto> Criteria);
