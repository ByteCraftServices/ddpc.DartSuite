using System.Text.Json;
using ddpc.DartSuite.ApiClient.Contracts;

namespace ddpc.DartSuite.Api.Services;

internal static class AutodartsMatchScoreMapper
{
    public static AutodartsMappedScoreResult MapScores(AutodartsMatchDetail match, string? homePlayerName, string? awayPlayerName)
    {
        var players = GetOrderedPlayers(match.RawJson);
        var slotScores = ParseSlotScores(match.RawJson, players);

        var homeSlot = ResolveSlot(players, homePlayerName);
        var awaySlot = ResolveSlot(players, awayPlayerName);
        var mappingSource = "fallback";

        if (homeSlot.HasValue && awaySlot.HasValue && homeSlot.Value != awaySlot.Value)
        {
            mappingSource = "home-away-name";
        }
        else if (homeSlot.HasValue && !awaySlot.HasValue && players.Count >= 2)
        {
            awaySlot = players.FirstOrDefault(x => x.Slot != homeSlot.Value)?.Slot;
            mappingSource = "home-name";
        }
        else if (!homeSlot.HasValue && awaySlot.HasValue && players.Count >= 2)
        {
            homeSlot = players.FirstOrDefault(x => x.Slot != awaySlot.Value)?.Slot;
            mappingSource = "away-name";
        }

        var firstSlot = players.FirstOrDefault()?.Slot ?? 0;
        var secondSlot = players.Skip(1).FirstOrDefault()?.Slot ?? (firstSlot == 0 ? 1 : 0);
        homeSlot ??= firstSlot;
        awaySlot ??= secondSlot;

        if (homeSlot == awaySlot)
        {
            homeSlot = firstSlot;
            awaySlot = secondSlot;
            mappingSource = "fallback";
        }

        var homeScore = slotScores.TryGetValue(homeSlot.Value, out var resolvedHomeScore)
            ? resolvedHomeScore
            : SlotScore.Empty;
        var awayScore = slotScores.TryGetValue(awaySlot.Value, out var resolvedAwayScore)
            ? resolvedAwayScore
            : SlotScore.Empty;

        var slot0 = players.FirstOrDefault(x => x.Slot == 0) ?? players.FirstOrDefault();
        var slot1 = players.FirstOrDefault(x => x.Slot == 1) ?? players.Skip(1).FirstOrDefault();

        return new AutodartsMappedScoreResult(
            homeScore.Legs,
            awayScore.Legs,
            homeScore.Sets,
            awayScore.Sets,
            homeScore.Points,
            awayScore.Points,
            slot0?.Name,
            slot1?.Name,
            mappingSource,
            homeSlot.Value,
            awaySlot.Value);
    }

    public static string[] GetOrderedPlayerNames(AutodartsMatchDetail match)
    {
        return GetOrderedPlayers(match.RawJson)
            .Select(player => player.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }

    private static Dictionary<int, SlotScore> ParseSlotScores(JsonElement rawJson, IReadOnlyList<ExternalPlayerSlot> players)
    {
        var scores = new Dictionary<int, SlotScore>();

        AddLegWinnersFromLegs(rawJson, players, scores);
        AddLegsFromStats(rawJson, scores);
        AddLegsFromGameScores(rawJson, scores);
        AddLegsAndSetsFromScores(rawJson, scores);
        AddCurrentPointsFromPlayers(rawJson, scores);

        return scores;
    }

    private static void AddCurrentPointsFromPlayers(JsonElement rawJson, Dictionary<int, SlotScore> scores)
    {
        if (!rawJson.TryGetProperty("players", out var playersElement) || playersElement.ValueKind != JsonValueKind.Array)
            return;

        var arrayIndex = 0;
        foreach (var player in playersElement.EnumerateArray())
        {
            if (player.ValueKind != JsonValueKind.Object)
            {
                arrayIndex++;
                continue;
            }

            var slot = arrayIndex;
            var points = TryGetInt(player, "score")
                         ?? TryGetInt(player, "points")
                         ?? TryGetInt(player, "remaining")
                         ?? TryGetInt(player, "remainingScore")
                         ?? TryGetInt(player, "currentScore");

            if (!points.HasValue && player.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                points = TryGetInt(state, "score")
                         ?? TryGetInt(state, "points")
                         ?? TryGetInt(state, "remaining")
                         ?? TryGetInt(state, "remainingScore")
                         ?? TryGetInt(state, "currentScore");
            }

            if (points.HasValue)
            {
                UpsertSlotScore(scores, slot, slotScore => slotScore with { Points = points.Value });
            }

            arrayIndex++;
        }
    }

    private static void AddLegWinnersFromLegs(JsonElement rawJson, IReadOnlyList<ExternalPlayerSlot> players, Dictionary<int, SlotScore> scores)
    {
        if (rawJson.TryGetProperty("legs", out var legsElement) && legsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var setOrLeg in legsElement.EnumerateArray())
            {
                if (setOrLeg.ValueKind == JsonValueKind.Array)
                {
                    foreach (var leg in setOrLeg.EnumerateArray())
                    {
                        CountLegWinner(leg, players, scores);
                    }
                }
                else if (setOrLeg.ValueKind == JsonValueKind.Object)
                {
                    CountLegWinner(setOrLeg, players, scores);
                }
            }
        }

        if (rawJson.TryGetProperty("sets", out var setsElement) && setsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var set in setsElement.EnumerateArray())
            {
                if (set.ValueKind != JsonValueKind.Object)
                    continue;

                if (set.TryGetProperty("legs", out var setLegs) && setLegs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var leg in setLegs.EnumerateArray())
                    {
                        CountLegWinner(leg, players, scores);
                    }
                }
            }
        }
    }

    private static void AddLegsFromStats(JsonElement rawJson, Dictionary<int, SlotScore> scores)
    {
        if (!rawJson.TryGetProperty("stats", out var statsElement) || statsElement.ValueKind != JsonValueKind.Array)
            return;

        for (var arrayIndex = 0; arrayIndex < statsElement.GetArrayLength() && arrayIndex < 2; arrayIndex++)
        {
            var playerStats = statsElement[arrayIndex];
            var legsWon = TryGetInt(playerStats, "legsWon")
                       ?? TryGetInt(playerStats, "legs_won")
                       ?? TryGetInt(playerStats, "legs");
            if (legsWon.HasValue)
            {
                UpsertSlotScore(scores, arrayIndex, slotScore => slotScore with { Legs = legsWon.Value });
            }
        }
    }

    private static void AddLegsFromGameScores(JsonElement rawJson, Dictionary<int, SlotScore> scores)
    {
        if (!rawJson.TryGetProperty("gameScores", out var gameScores) || gameScores.ValueKind != JsonValueKind.Array)
            return;

        for (var arrayIndex = 0; arrayIndex < gameScores.GetArrayLength() && arrayIndex < 2; arrayIndex++)
        {
            var score = TryGetInt(gameScores[arrayIndex]);
            if (score.HasValue)
            {
                UpsertSlotScore(scores, arrayIndex, slotScore => slotScore with { Points = score.Value });
            }
        }
    }

    private static void AddLegsAndSetsFromScores(JsonElement rawJson, Dictionary<int, SlotScore> scores)
    {
        if (!rawJson.TryGetProperty("scores", out var scoresElement) || scoresElement.ValueKind != JsonValueKind.Array)
            return;

        for (var arrayIndex = 0; arrayIndex < scoresElement.GetArrayLength() && arrayIndex < 2; arrayIndex++)
        {
            var legsWon = TryGetInt(scoresElement[arrayIndex], "legs");
            var setsWon = TryGetInt(scoresElement[arrayIndex], "sets");

            if (legsWon.HasValue || setsWon.HasValue)
            {
                UpsertSlotScore(scores, arrayIndex, slotScore => slotScore with
                {
                    Legs = legsWon ?? slotScore.Legs,
                    Sets = setsWon ?? slotScore.Sets
                });
            }
        }
    }

    private static void CountLegWinner(JsonElement leg, IReadOnlyList<ExternalPlayerSlot> players, Dictionary<int, SlotScore> scores)
    {
        var winnerSlot = TryResolveWinnerSlot(leg, players);
        if (!winnerSlot.HasValue && leg.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
        {
            winnerSlot = TryResolveWinnerSlot(result, players);
        }

        if (winnerSlot.HasValue)
        {
            UpsertSlotScore(scores, winnerSlot.Value, slotScore => slotScore with { Legs = slotScore.Legs + 1 });
        }
    }

    private static int? TryResolveWinnerSlot(JsonElement element, IReadOnlyList<ExternalPlayerSlot> players)
    {
        foreach (var candidate in new[] { "winner", "won", "winnerId", "winnerIndex" })
        {
            if (!element.TryGetProperty(candidate, out var property))
                continue;

            var winnerSlot = TryResolveSlotValue(property, players);
            if (winnerSlot.HasValue)
                return winnerSlot;
        }

        return null;
    }

    private static int? TryResolveSlotValue(JsonElement value, IReadOnlyList<ExternalPlayerSlot> players)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            var numericValue = value.GetInt32();
            if (numericValue >= 0 && numericValue < players.Count)
                return numericValue;

            var byIndex = players.FirstOrDefault(player => player.Index == numericValue);
            return byIndex?.Slot;
        }

        if (value.ValueKind != JsonValueKind.String)
            return null;

        var rawValue = value.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        if (int.TryParse(rawValue, out var numericString))
        {
            if (numericString >= 0 && numericString < players.Count)
                return numericString;

            var byNumericIndex = players.FirstOrDefault(player => player.Index == numericString);
            if (byNumericIndex is not null)
                return byNumericIndex.Slot;
        }

        var normalized = NormalizeName(rawValue);
        var byIdentity = players.FirstOrDefault(player =>
            (!string.IsNullOrWhiteSpace(player.Id) && string.Equals(player.Id, rawValue, StringComparison.OrdinalIgnoreCase))
            || player.Aliases.Any(alias => NormalizeName(alias) == normalized));
        return byIdentity?.Slot;
    }

    private static int? ResolveSlot(IReadOnlyList<ExternalPlayerSlot> players, string? playerName)
    {
        var normalizedName = NormalizeName(playerName);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return null;

        var match = players.FirstOrDefault(player => player.Aliases.Any(alias => NormalizeName(alias) == normalizedName));
        return match?.Slot;
    }

    private static IReadOnlyList<ExternalPlayerSlot> GetOrderedPlayers(JsonElement rawJson)
    {
        if (rawJson.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return [];

        if (!rawJson.TryGetProperty("players", out var playersElement) || playersElement.ValueKind != JsonValueKind.Array)
            return [];

        var players = new List<ExternalPlayerSlot>();
        var fallbackSlot = 0;

        foreach (var player in playersElement.EnumerateArray())
        {
            if (player.ValueKind == JsonValueKind.String)
            {
                players.Add(new ExternalPlayerSlot(fallbackSlot, fallbackSlot, null, player.GetString(), [player.GetString() ?? string.Empty]));
                fallbackSlot++;
                continue;
            }

            if (player.ValueKind != JsonValueKind.Object)
                continue;

            var index = TryGetInt(player, "index") ?? fallbackSlot;
            var slot = fallbackSlot;
            var aliases = ExtractAliases(player);
            players.Add(new ExternalPlayerSlot(slot, index, TryGetString(player, "id"), aliases.FirstOrDefault(), aliases));
            fallbackSlot++;
        }

        return players.ToArray();
    }

    private static string[] ExtractAliases(JsonElement player)
    {
        var aliases = new List<string?>
        {
            TryGetString(player, "name"),
            TryGetString(player, "displayName"),
            TryGetString(player, "accountName"),
            TryGetString(player, "username")
        };

        if (player.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            aliases.Add(TryGetString(user, "name"));
            aliases.Add(TryGetString(user, "displayName"));
            aliases.Add(TryGetString(user, "accountName"));
            aliases.Add(TryGetString(user, "username"));
        }

        return aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static void UpsertSlotScore(Dictionary<int, SlotScore> scores, int slot, Func<SlotScore, SlotScore> update)
    {
        var current = scores.TryGetValue(slot, out var existing)
            ? existing
            : SlotScore.Empty;
        scores[slot] = update(current);
    }

    private static int? TryGetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.String when int.TryParse(element.GetString(), out var number) => number,
            _ => null
        };
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private sealed record ExternalPlayerSlot(int Slot, int Index, string? Id, string? Name, string[] Aliases);

    private sealed record SlotScore(int Legs, int Sets, int? Points)
    {
        public static SlotScore Empty { get; } = new(0, 0, null);
    }
}

internal sealed record AutodartsMappedScoreResult(
    int HomeLegs,
    int AwayLegs,
    int HomeSets,
    int AwaySets,
    int? HomePoints,
    int? AwayPoints,
    string? ExternalPlayer1,
    string? ExternalPlayer2,
    string MappingSource,
    int HomeSlot,
    int AwaySlot);