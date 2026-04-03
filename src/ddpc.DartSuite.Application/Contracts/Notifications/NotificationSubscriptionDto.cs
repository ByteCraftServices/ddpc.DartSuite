namespace ddpc.DartSuite.Application.Contracts.Notifications;

public sealed record NotificationSubscriptionDto(
    Guid Id,
    Guid TournamentId,
    string UserAccountName,
    string Endpoint,
    string P256dh,
    string Auth,
    string NotificationPreference,
    DateTimeOffset CreatedUtc);
