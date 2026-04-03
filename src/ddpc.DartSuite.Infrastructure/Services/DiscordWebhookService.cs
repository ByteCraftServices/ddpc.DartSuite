using System.Net.Http.Json;
using System.Text.Json;
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

        await SendWebhookMessageAsync(tournament.DiscordWebhookUrl, content, cancellationToken);
    }

    public async Task SendTournamentStatusAsync(Guid tournamentId, string message, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament?.DiscordWebhookUrl is null) return;

        var displayText = tournament.DiscordWebhookDisplayText ?? tournament.Name;
        await SendWebhookMessageAsync(tournament.DiscordWebhookUrl, $"**{displayText}** - {message}", cancellationToken);
    }

    public async Task SendRoundSummaryAsync(Guid tournamentId, string phase, int round, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament?.DiscordWebhookUrl is null) return;

        var displayText = tournament.DiscordWebhookDisplayText ?? tournament.Name;
        await SendWebhookMessageAsync(tournament.DiscordWebhookUrl, $"**{displayText}** - Runde {round} ({phase}) abgeschlossen", cancellationToken);
    }

    public async Task<bool> TestWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendWebhookMessageAsync(webhookUrl, "DartSuite Webhook-Test erfolgreich!", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Discord webhook test failed for URL: {Url}", webhookUrl);
            return false;
        }
    }

    private async Task SendWebhookMessageAsync(string webhookUrl, string content, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("DiscordWebhook");
        var payload = new { content };
        var response = await client.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Discord webhook returned {StatusCode}", response.StatusCode);
        }
    }
}
