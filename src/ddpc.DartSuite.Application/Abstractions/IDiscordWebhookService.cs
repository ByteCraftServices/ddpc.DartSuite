namespace ddpc.DartSuite.Application.Abstractions;

public interface IDiscordWebhookService
{
    Task SendMatchResultAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default);
    Task SendTournamentStatusAsync(Guid tournamentId, string message, CancellationToken cancellationToken = default);
    Task SendRoundSummaryAsync(Guid tournamentId, string phase, int round, CancellationToken cancellationToken = default);
    Task<bool> TestWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default);
}
