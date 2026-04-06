using System.Text.Json;

namespace ddpc.DartSuite.Domain.Services;

public static class AutodartsMatchStatisticsMapper
{
    public static IReadOnlyList<MappedMatchPlayerStatistic> Map(JsonElement rawJson)
    {
        if (!rawJson.TryGetProperty("stats", out var statsElement) || statsElement.ValueKind != JsonValueKind.Array)
            return [];

        var scoreSnapshots = ReadScoreSnapshots(rawJson);
        var result = new List<MappedMatchPlayerStatistic>();
        var slot = 0;

        foreach (var statsEntry in statsElement.EnumerateArray())
        {
            if (statsEntry.ValueKind != JsonValueKind.Object)
            {
                slot++;
                continue;
            }

            var matchStats = statsEntry.TryGetProperty("matchStats", out var nested) && nested.ValueKind == JsonValueKind.Object
                ? nested
                : statsEntry;

            var dartsThrown = ReadInt(matchStats, "dartsThrown");
            var ownScore = scoreSnapshots.TryGetValue(slot, out var slotScore) ? slotScore : (ScoreSnapshot?)null;
            var opponentScore = TryGetOpponentScore(scoreSnapshots, slot);

            var legsWon = ReadNullableInt(matchStats, "legsWon", "wonLegs") ?? ownScore?.Legs ?? 0;
            var legsLost = ReadNullableInt(matchStats, "legsLost", "lostLegs") ?? opponentScore?.Legs ?? 0;
            var setsWon = ReadNullableInt(matchStats, "setsWon", "wonSets") ?? ownScore?.Sets ?? 0;
            var setsLost = ReadNullableInt(matchStats, "setsLost", "lostSets") ?? opponentScore?.Sets ?? 0;
            var totalLegs = Math.Max(0, legsWon + legsLost);

            var checkoutHits = ReadInt(matchStats, "checkoutsHit");
            var checkoutAttempts = ReadInt(matchStats, "checkouts", "checkoutAttempts");
            var checkoutPercent = ReadDouble(matchStats, "checkoutPercent");

            result.Add(new MappedMatchPlayerStatistic(
                Slot: slot,
                Average: ReadDouble(matchStats, "average"),
                First9Average: ReadDouble(matchStats, "first9Average"),
                DartsThrown: dartsThrown,
                LegsWon: legsWon,
                LegsLost: legsLost,
                SetsWon: setsWon,
                SetsLost: setsLost,
                HighestCheckout: ReadInt(matchStats, "checkoutPoints", "highestCheckout"),
                CheckoutPercent: checkoutPercent,
                CheckoutHits: checkoutHits,
                CheckoutAttempts: checkoutAttempts,
                Plus100: ReadInt(matchStats, "plus100"),
                Plus140: ReadInt(matchStats, "plus140"),
                Plus170: ReadInt(matchStats, "plus170"),
                Plus180: ReadInt(matchStats, "total180", "plus180"),
                Breaks: ReadInt(matchStats, "breaks"),
                AverageDartsPerLeg: totalLegs > 0 ? Math.Round((double)dartsThrown / totalLegs, 2) : 0,
                BestLegDarts: ReadInt(matchStats, "bestLegDarts"),
                WorstLegDarts: ReadInt(matchStats, "worstLegDarts"),
                TonPlusCheckouts: ReadInt(matchStats, "tonPlusCheckouts"),
                DoubleQuota: checkoutPercent,
                TotalPoints: ReadInt(matchStats, "score"),
                HighestRoundScore: ReadNullableInt(matchStats, "highestRoundScore")));

            slot++;
        }

        return result;
    }

    private static Dictionary<int, ScoreSnapshot> ReadScoreSnapshots(JsonElement rawJson)
    {
        var snapshots = new Dictionary<int, ScoreSnapshot>();

        if (!rawJson.TryGetProperty("scores", out var scoresElement) || scoresElement.ValueKind != JsonValueKind.Array)
            return snapshots;

        var index = 0;
        foreach (var scoreEntry in scoresElement.EnumerateArray())
        {
            if (scoreEntry.ValueKind == JsonValueKind.Object)
            {
                snapshots[index] = new ScoreSnapshot(
                    ReadInt(scoreEntry, "legs"),
                    ReadInt(scoreEntry, "sets"));
            }

            index++;
        }

        return snapshots;
    }

    private static ScoreSnapshot? TryGetOpponentScore(IReadOnlyDictionary<int, ScoreSnapshot> scoreSnapshots, int slot)
    {
        foreach (var entry in scoreSnapshots)
        {
            if (entry.Key != slot)
                return entry.Value;
        }

        return null;
    }

    private static int ReadInt(JsonElement source, params string[] names)
    {
        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return 0;
    }

    private static int? ReadNullableInt(JsonElement source, params string[] names)
    {
        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static double ReadDouble(JsonElement source, params string[] names)
    {
        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return 0;
    }
}

internal readonly record struct ScoreSnapshot(int Legs, int Sets);

public sealed record MappedMatchPlayerStatistic(
    int Slot,
    double Average,
    double First9Average,
    int DartsThrown,
    int LegsWon,
    int LegsLost,
    int SetsWon,
    int SetsLost,
    int HighestCheckout,
    double CheckoutPercent,
    int CheckoutHits,
    int CheckoutAttempts,
    int Plus100,
    int Plus140,
    int Plus170,
    int Plus180,
    int Breaks,
    double AverageDartsPerLeg,
    int BestLegDarts,
    int WorstLegDarts,
    int TonPlusCheckouts,
    double DoubleQuota,
    int TotalPoints,
    int? HighestRoundScore);