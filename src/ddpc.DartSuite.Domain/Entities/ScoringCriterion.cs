using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

public sealed class ScoringCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public ScoringCriterionType Type { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
}
