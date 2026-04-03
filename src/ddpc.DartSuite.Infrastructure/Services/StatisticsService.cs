using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class StatisticsService(DartSuiteDbContext dbContext) : IStatisticsService
{
    public async Task<IReadOnlyList<MatchPlayerStatisticDto>> GetTournamentStatisticsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var matchIds = await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var stats = await dbContext.MatchPlayerStatistics.AsNoTracking()
            .Where(x => matchIds.Contains(x.MatchId))
            .ToListAsync(cancellationToken);

        var participantIds = stats.Select(s => s.ParticipantId).Distinct().ToList();
        var participants = await dbContext.Participants.AsNoTracking()
            .Where(p => participantIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        return stats.Select(s => ToDto(s, participants.GetValueOrDefault(s.ParticipantId, ""))).ToList();
    }

    public async Task<IReadOnlyList<MatchPlayerStatisticDto>> GetParticipantStatisticsAsync(Guid tournamentId, Guid participantId, CancellationToken cancellationToken = default)
    {
        var matchIds = await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var stats = await dbContext.MatchPlayerStatistics.AsNoTracking()
            .Where(x => matchIds.Contains(x.MatchId) && x.ParticipantId == participantId)
            .ToListAsync(cancellationToken);

        var participant = await dbContext.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken);

        return stats.Select(s => ToDto(s, participant?.DisplayName ?? "")).ToList();
    }

    public async Task<MatchPlayerStatisticDto?> GetLiveStatisticsAsync(Guid matchId, Guid participantId, CancellationToken cancellationToken = default)
    {
        var stat = await dbContext.MatchPlayerStatistics.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MatchId == matchId && x.ParticipantId == participantId, cancellationToken);

        if (stat is null) return null;

        var participant = await dbContext.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken);

        return ToDto(stat, participant?.DisplayName ?? "");
    }

    private static MatchPlayerStatisticDto ToDto(MatchPlayerStatistic s, string participantName) => new(
        s.Id, s.MatchId, s.ParticipantId, participantName,
        s.Average, s.First9Average, s.DartsThrown, s.LegsWon, s.LegsLost,
        s.SetsWon, s.SetsLost, s.HighestCheckout, s.CheckoutPercent,
        s.CheckoutHits, s.CheckoutAttempts, s.Plus100, s.Plus140, s.Plus170, s.Plus180,
        s.Breaks, s.AverageDartsPerLeg, s.BestLegDarts, s.WorstLegDarts,
        s.TonPlusCheckouts, s.DoubleQuota, s.TotalPoints, s.HighestRoundScore);
}
