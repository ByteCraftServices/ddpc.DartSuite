using ddpc.DartSuite.Application.Contracts.Matches;

namespace ddpc.DartSuite.Application.Abstractions;

public interface ISchedulingService
{
    Task<IReadOnlyList<MatchDto>> CalculateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchDto>> RecalculateDelaysAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<MatchDto?> UpdateMatchTimingAsync(Guid matchId, DateTimeOffset? actualStart, DateTimeOffset? actualEnd, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchDto>> GetBoardScheduleAsync(Guid boardId, CancellationToken cancellationToken = default);
}
