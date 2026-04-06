using ddpc.DartSuite.Application.Contracts.Notifications;
using ddpc.DartSuite.Application.Contracts.Tournaments;

namespace ddpc.DartSuite.Application.Abstractions;

public interface ITournamentManagementService
{
    Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentDto>> GetTournamentsByHostAsync(string host, CancellationToken cancellationToken = default);
    Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TournamentDto?> GetTournamentByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<TournamentDto> CreateTournamentAsync(CreateTournamentRequest request, CancellationToken cancellationToken = default);
    Task<TournamentDto?> UpdateTournamentAsync(UpdateTournamentRequest request, CancellationToken cancellationToken = default);
    Task<TournamentDto?> SetLockedAsync(Guid tournamentId, bool locked, CancellationToken cancellationToken = default);

    // Participants
    Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantDto>> SearchParticipantsAsync(string query, CancellationToken cancellationToken = default);
    Task<ParticipantDto> AddParticipantAsync(AddParticipantRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantDto?> UpdateParticipantAsync(UpdateParticipantRequest request, CancellationToken cancellationToken = default);
    Task<bool> RemoveParticipantAsync(Guid tournamentId, Guid participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantDto>> AssignSeedPotsAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    // Rounds
    Task<IReadOnlyList<TournamentRoundDto>> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TournamentRoundDto> SaveRoundAsync(SaveTournamentRoundRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoundAsync(Guid tournamentId, string phase, int roundNumber, CancellationToken cancellationToken = default);

    // Status
    Task<TournamentDto?> UpdateStatusAsync(Guid tournamentId, string status, CancellationToken cancellationToken = default);
    Task<bool> DeleteTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    // Teams
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamDto>> SaveTeamsAsync(SaveTeamsRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteTeamAsync(Guid tournamentId, Guid teamId, CancellationToken cancellationToken = default);

    // Scoring
    Task<IReadOnlyList<ScoringCriterionDto>> GetScoringCriteriaAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoringCriterionDto>> SaveScoringCriteriaAsync(SaveScoringCriteriaRequest request, CancellationToken cancellationToken = default);

    // Notifications (#14)
    Task<IReadOnlyList<NotificationSubscriptionDto>> GetNotificationSubscriptionsAsync(Guid tournamentId, string userAccountName, CancellationToken cancellationToken = default);
    Task<NotificationSubscriptionDto> SubscribeNotificationsAsync(CreateNotificationSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeNotificationsAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    // View Preferences (#15)
    Task<UserViewPreferenceDto?> GetUserViewPreferenceAsync(string userAccountName, string viewContext, CancellationToken cancellationToken = default);
    Task<UserViewPreferenceDto> SaveUserViewPreferenceAsync(string userAccountName, string viewContext, string settingsJson, CancellationToken cancellationToken = default);
}