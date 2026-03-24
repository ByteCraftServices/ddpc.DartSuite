namespace ddpc.DartSuite.Domain.Entities;

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public required string Name { get; set; }
    public int? GroupNumber { get; set; }
}
