using ddpc.DartSuite.Application.Contracts.Notifications;

namespace ddpc.DartSuite.Application.Abstractions;

public interface INotificationService
{
    Task SendMatchStartingAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default);
    Task SendMatchFinishedAsync(Guid tournamentId, Guid matchId, CancellationToken cancellationToken = default);
    Task SendRoundAdvancedAsync(Guid tournamentId, string phase, int round, CancellationToken cancellationToken = default);
    Task SendTournamentEventAsync(TournamentEventDto eventDto, CancellationToken cancellationToken = default);
    Task SendBrowserPushAsync(Guid subscriptionId, string title, string body, CancellationToken cancellationToken = default);
}
