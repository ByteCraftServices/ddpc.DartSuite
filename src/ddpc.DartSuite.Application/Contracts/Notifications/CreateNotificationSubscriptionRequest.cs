namespace ddpc.DartSuite.Application.Contracts.Notifications;

public sealed record CreateNotificationSubscriptionRequest(
    Guid TournamentId,
    string UserAccountName,
    string Endpoint,
    string P256dh,
    string Auth,
    string NotificationPreference);
