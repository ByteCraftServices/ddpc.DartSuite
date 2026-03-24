using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;

namespace ddpc.DartSuite.Application.Abstractions;

public interface IMatchManagementService
{
    Task<MatchDto?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchDto>> GetMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchDto>> GenerateKnockoutPlanAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchDto>> GenerateGroupPhaseAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupStandingDto>> GetGroupStandingsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<MatchDto?> ReportResultAsync(ReportMatchResultRequest request, CancellationToken cancellationToken = default);
    Task<MatchDto?> SyncMatchFromExternalAsync(Guid matchId, int homeLegs, int awayLegs, int homeSets, int awaySets, bool finished, CancellationToken cancellationToken = default);
    Task<MatchDto?> AssignBoardAsync(Guid matchId, Guid boardId, CancellationToken cancellationToken = default);
    Task<MatchDto?> SwapParticipantsAsync(Guid matchId, Guid participantId, Guid newParticipantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchDto>> GenerateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<MatchDto?> UpdateMatchScheduleAsync(Guid matchId, DateTimeOffset? startTime, bool lockTime, Guid? boardId, bool lockBoard, CancellationToken cancellationToken = default);
    Task<MatchDto?> ToggleMatchTimeLockAsync(Guid matchId, bool locked, CancellationToken cancellationToken = default);
    Task<MatchDto?> ToggleMatchBoardLockAsync(Guid matchId, bool locked, CancellationToken cancellationToken = default);
    Task<MatchDto?> ResetMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    MatchPredictionDto GetPrediction(int targetLegs, int homeLegs, int awayLegs, int homeScore, int awayScore, TimeSpan elapsed);
}