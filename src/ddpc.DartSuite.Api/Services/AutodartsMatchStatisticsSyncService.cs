using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ddpc.DartSuite.Api.Services;

public static class AutodartsMatchStatisticsSyncService
{
    public static async Task<StatisticsSyncResult> UpsertFromRawAsync(
        DartSuiteDbContext dbContext,
        Guid matchId,
        Guid homeParticipantId,
        Guid awayParticipantId,
        JsonElement rawJson,
        CancellationToken cancellationToken)
    {
        var mapped = AutodartsMatchStatisticsMapper.Map(rawJson);
        var changed = false;
        var now = DateTimeOffset.UtcNow;
        var trackedParticipantIds = new[] { homeParticipantId, awayParticipantId };

        var existingStatistics = await dbContext.MatchPlayerStatistics
            .Where(x => x.MatchId == matchId && trackedParticipantIds.Contains(x.ParticipantId))
            .ToDictionaryAsync(x => x.ParticipantId, cancellationToken);

        var incomingByParticipant = mapped
            .Select(item => new
            {
                ParticipantId = item.Slot switch
                {
                    0 => homeParticipantId,
                    1 => awayParticipantId,
                    _ => Guid.Empty
                },
                Item = item
            })
            .Where(x => x.ParticipantId != Guid.Empty)
            .ToDictionary(x => x.ParticipantId, x => x.Item);

        if (incomingByParticipant.Count == 0)
        {
            if (existingStatistics.Count == 0)
                return new StatisticsSyncResult(0, false);

            dbContext.MatchPlayerStatistics.RemoveRange(existingStatistics.Values);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new StatisticsSyncResult(0, true);
        }

        foreach (var incoming in incomingByParticipant)
        {
            var participantId = incoming.Key;
            var item = incoming.Value;

            existingStatistics.TryGetValue(participantId, out var existing);

            if (existing is null)
            {
                existing = new MatchPlayerStatistic
                {
                    MatchId = matchId,
                    ParticipantId = participantId,
                    CreatedUtc = now
                };
                dbContext.MatchPlayerStatistics.Add(existing);
                changed = true;
            }

            changed |= Apply(existing, item);
        }

        var staleStatistics = existingStatistics
            .Where(x => !incomingByParticipant.ContainsKey(x.Key))
            .Select(x => x.Value)
            .ToList();
        if (staleStatistics.Count > 0)
        {
            dbContext.MatchPlayerStatistics.RemoveRange(staleStatistics);
            changed = true;
        }

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);

        return new StatisticsSyncResult(incomingByParticipant.Count, changed);
    }

    private static bool Apply(MatchPlayerStatistic target, MappedMatchPlayerStatistic source)
    {
        var changed = false;

        changed |= Set(() => target.Average, v => target.Average = v, source.Average);
        changed |= Set(() => target.First9Average, v => target.First9Average = v, source.First9Average);
        changed |= Set(() => target.DartsThrown, v => target.DartsThrown = v, source.DartsThrown);
        changed |= Set(() => target.LegsWon, v => target.LegsWon = v, source.LegsWon);
        changed |= Set(() => target.LegsLost, v => target.LegsLost = v, source.LegsLost);
        changed |= Set(() => target.SetsWon, v => target.SetsWon = v, source.SetsWon);
        changed |= Set(() => target.SetsLost, v => target.SetsLost = v, source.SetsLost);
        changed |= Set(() => target.HighestCheckout, v => target.HighestCheckout = v, source.HighestCheckout);
        changed |= Set(() => target.CheckoutPercent, v => target.CheckoutPercent = v, source.CheckoutPercent);
        changed |= Set(() => target.CheckoutHits, v => target.CheckoutHits = v, source.CheckoutHits);
        changed |= Set(() => target.CheckoutAttempts, v => target.CheckoutAttempts = v, source.CheckoutAttempts);
        changed |= Set(() => target.Plus100, v => target.Plus100 = v, source.Plus100);
        changed |= Set(() => target.Plus140, v => target.Plus140 = v, source.Plus140);
        changed |= Set(() => target.Plus170, v => target.Plus170 = v, source.Plus170);
        changed |= Set(() => target.Plus180, v => target.Plus180 = v, source.Plus180);
        changed |= Set(() => target.Breaks, v => target.Breaks = v, source.Breaks);
        changed |= Set(() => target.AverageDartsPerLeg, v => target.AverageDartsPerLeg = v, source.AverageDartsPerLeg);
        changed |= Set(() => target.BestLegDarts, v => target.BestLegDarts = v, source.BestLegDarts);
        changed |= Set(() => target.WorstLegDarts, v => target.WorstLegDarts = v, source.WorstLegDarts);
        changed |= Set(() => target.TonPlusCheckouts, v => target.TonPlusCheckouts = v, source.TonPlusCheckouts);
        changed |= Set(() => target.DoubleQuota, v => target.DoubleQuota = v, source.DoubleQuota);
        changed |= Set(() => target.TotalPoints, v => target.TotalPoints = v, source.TotalPoints);
        changed |= SetNullable(() => target.HighestRoundScore, v => target.HighestRoundScore = v, source.HighestRoundScore);

        return changed;
    }

    private static bool Set<T>(Func<T> get, Action<T> set, T source) where T : struct, IEquatable<T>
    {
        var target = get();
        if (target.Equals(source))
            return false;

        set(source);
        return true;
    }

    private static bool SetNullable<T>(Func<T?> get, Action<T?> set, T? source) where T : struct, IEquatable<T>
    {
        var target = get();
        if (target.HasValue == source.HasValue && (!target.HasValue || target.Value.Equals(source!.Value)))
            return false;

        set(source);
        return true;
    }
}

public sealed record StatisticsSyncResult(int ProcessedEntries, bool Changed);