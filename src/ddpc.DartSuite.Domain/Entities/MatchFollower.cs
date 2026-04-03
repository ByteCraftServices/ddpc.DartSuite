namespace ddpc.DartSuite.Domain.Entities;

/// <summary>Tracks which matches a user is following for notifications.</summary>
public sealed class MatchFollower
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public string UserAccountName { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
