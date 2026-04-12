using System.Net.Http.Json;
using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class DiscordWebhookService(
    DartSuiteDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<DiscordWebhookService> logger) : IDiscordWebhookService
{
    private static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    ];

    public async Task SendMatchResultAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament?.DiscordWebhookUrl is null) return;

        var match = await dbContext.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return;

        var homePlayer = await dbContext.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == match.HomeParticipantId, cancellationToken);
        var awayPlayer = await dbContext.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == match.AwayParticipantId, cancellationToken);

        var displayText = tournament.DiscordWebhookDisplayText ?? tournament.Name;
        var content = $"**{displayText}** - Ergebnis: {homePlayer?.DisplayName ?? "?"} {match.HomeLegs}:{match.AwayLegs} {awayPlayer?.DisplayName ?? "?"}";

        await SendWebhookMessageAsync(tournament.DiscordWebhookUrl, content, cancellationToken, tournamentId, matchId, "MatchResult");
    }

    public async Task SendTournamentStatusAsync(Guid tournamentId, string message, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament?.DiscordWebhookUrl is null) return;

        var displayText = tournament.DiscordWebhookDisplayText ?? tournament.Name;
        await SendWebhookMessageAsync(tournament.DiscordWebhookUrl, $"**{displayText}** - {message}", cancellationToken, tournamentId, null, "TournamentStatus");
    }

    public async Task SendRoundSummaryAsync(Guid tournamentId, string phase, int round, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament?.DiscordWebhookUrl is null) return;

        var displayText = tournament.DiscordWebhookDisplayText ?? tournament.Name;
        await SendWebhookMessageAsync(tournament.DiscordWebhookUrl, $"**{displayText}** - Runde {round} ({phase}) abgeschlossen", cancellationToken, tournamentId, null, "RoundSummary");
    }

    public async Task<bool> TestWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        return await SendWebhookMessageAsync(webhookUrl, "DartSuite Webhook-Test erfolgreich!", cancellationToken, null, null, "WebhookTest");
    }

    private async Task<bool> SendWebhookMessageAsync(
        string webhookUrl,
        string content,
        CancellationToken cancellationToken,
        Guid? tournamentId,
        Guid? matchId,
        string eventType)
    {
        var payload = new { content };
        using var client = httpClientFactory.CreateClient("DiscordWebhook");

        for (var attempt = 1; attempt <= RetryBackoff.Length + 1; attempt++)
        {
            try
            {
                using var response = await client.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation(
                        "Discord webhook delivered event {EventType} tournament={TournamentId} match={MatchId} attempt={Attempt}",
                        eventType,
                        tournamentId,
                        matchId,
                        attempt);
                    return true;
                }

                var statusCode = (int)response.StatusCode;
                var isPermanentClientError = statusCode is >= 400 and < 500 && statusCode != 429;
                if (isPermanentClientError)
                {
                    logger.LogError(
                        "Discord webhook permanent failure event={EventType} tournament={TournamentId} match={MatchId} status={StatusCode} attempt={Attempt}",
                        eventType,
                        tournamentId,
                        matchId,
                        statusCode,
                        attempt);
                    return false;
                }

                if (attempt > RetryBackoff.Length)
                {
                    logger.LogError(
                        "Discord webhook failed after retries event={EventType} tournament={TournamentId} match={MatchId} status={StatusCode} attempts={Attempts}",
                        eventType,
                        tournamentId,
                        matchId,
                        statusCode,
                        attempt);
                    return false;
                }

                var retryDelay = ResolveRetryDelay(response, attempt);
                logger.LogWarning(
                    "Discord webhook transient failure event={EventType} tournament={TournamentId} match={MatchId} status={StatusCode} attempt={Attempt} retryInMs={RetryDelayMs}",
                    eventType,
                    tournamentId,
                    matchId,
                    statusCode,
                    attempt,
                    (int)retryDelay.TotalMilliseconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt > RetryBackoff.Length)
                {
                    logger.LogError(
                        "Discord webhook timeout after retries event={EventType} tournament={TournamentId} match={MatchId} attempts={Attempts}",
                        eventType,
                        tournamentId,
                        matchId,
                        attempt);
                    return false;
                }

                var retryDelay = RetryBackoff[attempt - 1];
                logger.LogWarning(
                    "Discord webhook timeout event={EventType} tournament={TournamentId} match={MatchId} attempt={Attempt} retryInMs={RetryDelayMs}",
                    eventType,
                    tournamentId,
                    matchId,
                    attempt,
                    (int)retryDelay.TotalMilliseconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                if (attempt > RetryBackoff.Length)
                {
                    logger.LogError(
                        ex,
                        "Discord webhook network failure after retries event={EventType} tournament={TournamentId} match={MatchId} attempts={Attempts}",
                        eventType,
                        tournamentId,
                        matchId,
                        attempt);
                    return false;
                }

                var retryDelay = RetryBackoff[attempt - 1];
                logger.LogWarning(
                    ex,
                    "Discord webhook network failure event={EventType} tournament={TournamentId} match={MatchId} attempt={Attempt} retryInMs={RetryDelayMs}",
                    eventType,
                    tournamentId,
                    matchId,
                    attempt,
                    (int)retryDelay.TotalMilliseconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Discord webhook unexpected failure event={EventType} tournament={TournamentId} match={MatchId} attempt={Attempt}",
                    eventType,
                    tournamentId,
                    matchId,
                    attempt);
                return false;
            }
        }

        return false;
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;

        if (retryAfter?.Date is DateTimeOffset date)
        {
            var diff = date - DateTimeOffset.UtcNow;
            if (diff > TimeSpan.Zero)
                return diff;
        }

        return RetryBackoff[Math.Clamp(attempt - 1, 0, RetryBackoff.Length - 1)];
    }
}
