using ddpc.DartSuite.Application.Contracts.Matches;

namespace ddpc.DartSuite.Application.Abstractions;

public interface IStatisticsService
{
    Task<IReadOnlyList<MatchPlayerStatisticDto>> GetTournamentStatisticsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchPlayerStatisticDto>> GetParticipantStatisticsAsync(Guid tournamentId, Guid participantId, CancellationToken cancellationToken = default);
    Task<MatchPlayerStatisticDto?> GetLiveStatisticsAsync(Guid matchId, Guid participantId, CancellationToken cancellationToken = default);
}
