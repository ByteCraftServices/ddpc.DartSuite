using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

/// <summary>Browser push notification subscription for a participant.</summary>
public sealed class NotificationSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public string UserAccountName { get; set; } = string.Empty;
    public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.OwnMatches;

    // Web Push fields
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
