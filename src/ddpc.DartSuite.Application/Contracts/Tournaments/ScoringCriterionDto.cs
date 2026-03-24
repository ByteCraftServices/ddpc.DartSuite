namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record ScoringCriterionDto(
    Guid Id,
    string Type,
    int Priority,
    bool IsEnabled);
