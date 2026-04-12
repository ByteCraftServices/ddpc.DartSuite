using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Notifications;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Configuration;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class NotificationService(
    DartSuiteDbContext dbContext,
    IDiscordWebhookService discordWebhookService,
    IOptions<VapidOptions> vapidOptions,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly VapidOptions _vapid = vapidOptions.Value;
    public async Task SendMatchStartingAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return;

        var homePlayer = await dbContext.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == match.HomeParticipantId, cancellationToken);
        var awayPlayer = await dbContext.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == match.AwayParticipantId, cancellationToken);

        var title = "Match startet";
        var body = $"{homePlayer?.DisplayName ?? "?"} vs {awayPlayer?.DisplayName ?? "?"}";

        // Find all recipients: subscribers matching this match
        var subscriptions = await dbContext.NotificationSubscriptions.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        foreach (var sub in subscriptions)
        {
            var shouldNotify = sub.NotificationPreference.HasFlag(NotificationPreference.AllMatches)
                || (sub.NotificationPreference.HasFlag(NotificationPreference.OwnMatches) && IsOwnMatch(sub.UserAccountName, homePlayer?.AccountName, awayPlayer?.AccountName))
                || (sub.NotificationPreference.HasFlag(NotificationPreference.FollowedMatches) && await IsFollowedMatchAsync(sub.UserAccountName, matchId, cancellationToken));

            if (shouldNotify)
            {
                await SendBrowserPushAsync(sub.Id, title, body, cancellationToken);
            }
        }
    }

    public async Task SendMatchFinishedAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return;

        await discordWebhookService.SendMatchResultAsync(tournamentId, matchId, cancellationToken);

        var homePlayer = await dbContext.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == match.HomeParticipantId, cancellationToken);
        var awayPlayer = await dbContext.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == match.AwayParticipantId, cancellationToken);

        var title = "Match beendet";
        var body = $"{homePlayer?.DisplayName ?? "?"} {match.HomeLegs}:{match.AwayLegs} {awayPlayer?.DisplayName ?? "?"}";

        var subscriptions = await dbContext.NotificationSubscriptions.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        foreach (var sub in subscriptions)
        {
            var shouldNotify = sub.NotificationPreference.HasFlag(NotificationPreference.AllMatches)
                || (sub.NotificationPreference.HasFlag(NotificationPreference.OwnMatches) && IsOwnMatch(sub.UserAccountName, homePlayer?.AccountName, awayPlayer?.AccountName))
                || (sub.NotificationPreference.HasFlag(NotificationPreference.FollowedMatches) && await IsFollowedMatchAsync(sub.UserAccountName, matchId, cancellationToken));

            if (shouldNotify)
            {
                await SendBrowserPushAsync(sub.Id, title, body, cancellationToken);
            }
        }
    }

    public async Task SendRoundAdvancedAsync(Guid tournamentId, string phase, int round, CancellationToken cancellationToken = default)
    {
        await discordWebhookService.SendRoundSummaryAsync(tournamentId, phase, round, cancellationToken);
    }

    public Task SendTournamentEventAsync(TournamentEventDto eventDto, CancellationToken cancellationToken = default)
    {
        // Placeholder for generic tournament event broadcasting
        logger.LogInformation("Tournament event: {EventType} for tournament {TournamentId}", eventDto.EventType, eventDto.TournamentId);
        return Task.CompletedTask;
    }

    public async Task SendBrowserPushAsync(Guid subscriptionId, string title, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_vapid.PublicKey) || string.IsNullOrEmpty(_vapid.PrivateKey))
        {
            logger.LogWarning("VAPID keys not configured — skipping push notification for subscription {SubscriptionId}", subscriptionId);
            return;
        }

        var sub = await dbContext.NotificationSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);
        if (sub is null || string.IsNullOrEmpty(sub.Endpoint)) return;

        var pushSubscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
        var vapidDetails = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
        var client = new WebPushClient();

        var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body, url = "/" });

        try
        {
            await client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            logger.LogInformation("Push notification sent to subscription {SubscriptionId}: {Title}", subscriptionId, title);
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Subscription expired — remove it
            var toRemove = await dbContext.NotificationSubscriptions.FindAsync([subscriptionId], cancellationToken);
            if (toRemove is not null)
            {
                dbContext.NotificationSubscriptions.Remove(toRemove);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            logger.LogInformation("Removed expired push subscription {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send push notification to subscription {SubscriptionId}", subscriptionId);
        }
    }

    private static bool IsOwnMatch(string userAccountName, string? homeAccount, string? awayAccount)
    {
        return string.Equals(userAccountName, homeAccount, StringComparison.OrdinalIgnoreCase)
            || string.Equals(userAccountName, awayAccount, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsFollowedMatchAsync(string userAccountName, Guid matchId, CancellationToken cancellationToken)
    {
        return await dbContext.MatchFollowers.AnyAsync(
            x => x.MatchId == matchId && x.UserAccountName == userAccountName, cancellationToken);
    }
}
