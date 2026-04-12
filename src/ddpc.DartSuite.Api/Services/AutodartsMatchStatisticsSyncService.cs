using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.ApiClient.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ddpc.DartSuite.Api.Services;

public static class AutodartsMatchStatisticsSyncService
{
    private static readonly string[] TimestampPropertyCandidates =
    [
        "updatedAt",
        "lastUpdatedAt",
        "timestamp",
        "eventTimestamp",
        "occurredAt",
        "createdAt"
    ];

    public static async Task<StatisticsSyncResult> UpsertFromRawAsync(
        DartSuiteDbContext dbContext,
        Guid matchId,
        Guid homeParticipantId,
        Guid awayParticipantId,
        JsonElement rawJson,
        DateTimeOffset senderUtc,
        string? homeParticipantName,
        string? awayParticipantName,
        CancellationToken cancellationToken)
    {
        var mapped = AutodartsMatchStatisticsMapper.Map(rawJson);
        var changed = false;
        var effectiveSenderUtc = senderUtc.ToUniversalTime();
        var trackedParticipantIds = new[] { homeParticipantId, awayParticipantId };

        var existingStatistics = await dbContext.MatchPlayerStatistics
            .Where(x => x.MatchId == matchId && trackedParticipantIds.Contains(x.ParticipantId))
            .ToDictionaryAsync(x => x.ParticipantId, cancellationToken);

        var slotToParticipantId = ResolveSlotToParticipantMap(
            rawJson,
            homeParticipantId,
            awayParticipantId,
            homeParticipantName,
            awayParticipantName);

        var incomingByParticipant = mapped
            .Select(item => new
            {
                ParticipantId = slotToParticipantId.TryGetValue(item.Slot, out var participantId) ? participantId : Guid.Empty,
                Item = item
            })
            .Where(x => x.ParticipantId != Guid.Empty)
            .ToDictionary(x => x.ParticipantId, x => x.Item);

        if (incomingByParticipant.Count == 0)
        {
            if (existingStatistics.Count == 0)
                return new StatisticsSyncResult(0, false, effectiveSenderUtc);

            var removable = existingStatistics
                .Values
                .Where(x => x.CreatedUtc <= effectiveSenderUtc)
                .ToList();

            if (removable.Count == 0)
                return new StatisticsSyncResult(0, false, effectiveSenderUtc);

            dbContext.MatchPlayerStatistics.RemoveRange(removable);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new StatisticsSyncResult(0, true, effectiveSenderUtc);
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
                    CreatedUtc = effectiveSenderUtc
                };
                dbContext.MatchPlayerStatistics.Add(existing);
                changed = true;
            }

            if (existing.CreatedUtc > effectiveSenderUtc)
                continue;

            var applied = Apply(existing, item);
            if (existing.CreatedUtc != effectiveSenderUtc)
            {
                existing.CreatedUtc = effectiveSenderUtc;
                applied = true;
            }

            changed |= applied;
        }

        var staleStatistics = existingStatistics
            .Where(x => !incomingByParticipant.ContainsKey(x.Key) && x.Value.CreatedUtc <= effectiveSenderUtc)
            .Select(x => x.Value)
            .ToList();
        if (staleStatistics.Count > 0)
        {
            dbContext.MatchPlayerStatistics.RemoveRange(staleStatistics);
            changed = true;
        }

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);

        return new StatisticsSyncResult(incomingByParticipant.Count, changed, effectiveSenderUtc);
    }

    public static DateTimeOffset ResolveSenderUtc(JsonElement rawJson, DateTimeOffset fallbackUtc)
    {
        var fallback = fallbackUtc.ToUniversalTime();

        if (TryReadTimestamp(rawJson, out var timestampUtc))
            return timestampUtc;

        if (rawJson.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in new[] { "meta", "event", "match", "state", "data" })
            {
                if (!rawJson.TryGetProperty(candidate, out var nested))
                    continue;

                if (TryReadTimestamp(nested, out timestampUtc))
                    return timestampUtc;
            }
        }

        return fallback;
    }

    private static bool TryReadTimestamp(JsonElement element, out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var propertyName in TimestampPropertyCandidates)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var parsedString))
            {
                timestampUtc = parsedString.ToUniversalTime();
                return true;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                if (value.TryGetInt64(out var unixCandidate))
                {
                    timestampUtc = unixCandidate > 1_000_000_000_000
                        ? DateTimeOffset.FromUnixTimeMilliseconds(unixCandidate).ToUniversalTime()
                        : DateTimeOffset.FromUnixTimeSeconds(unixCandidate).ToUniversalTime();
                    return true;
                }

                if (value.TryGetDouble(out var unixDouble))
                {
                    var ms = (long)Math.Round(unixDouble * 1000d, MidpointRounding.AwayFromZero);
                    timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToUniversalTime();
                    return true;
                }
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<int, Guid> ResolveSlotToParticipantMap(
        JsonElement rawJson,
        Guid homeParticipantId,
        Guid awayParticipantId,
        string? homeParticipantName,
        string? awayParticipantName)
    {
        var fallback = new Dictionary<int, Guid>
        {
            [0] = homeParticipantId,
            [1] = awayParticipantId
        };

        var match = TryBuildMatchDetail(rawJson);
        if (match is null)
            return fallback;

        var mappedScores = AutodartsMatchScoreMapper.MapScores(match, homeParticipantName, awayParticipantName);
        if (mappedScores.HomeSlot == mappedScores.AwaySlot)
            return fallback;

        return new Dictionary<int, Guid>
        {
            [mappedScores.HomeSlot] = homeParticipantId,
            [mappedScores.AwaySlot] = awayParticipantId
        };
    }

    private static AutodartsMatchDetail? TryBuildMatchDetail(JsonElement rawJson)
    {
        if (rawJson.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            return new AutodartsMatchDetail(
                TryGetString(rawJson, "id") ?? string.Empty,
                TryGetString(rawJson, "variant"),
                TryGetString(rawJson, "gameMode"),
                rawJson.TryGetProperty("finished", out var finishedElement) && finishedElement.ValueKind == JsonValueKind.True,
                GetElementOrNull(rawJson, "players"),
                GetElementOrNull(rawJson, "turns"),
                GetElementOrNull(rawJson, "legs"),
                GetElementOrNull(rawJson, "sets"),
                GetElementOrNull(rawJson, "stats"),
                rawJson.Clone());
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement? GetElementOrNull(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            return value.Clone();

        return null;
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

public sealed record StatisticsSyncResult(int ProcessedEntries, bool Changed, DateTimeOffset SenderUtc);