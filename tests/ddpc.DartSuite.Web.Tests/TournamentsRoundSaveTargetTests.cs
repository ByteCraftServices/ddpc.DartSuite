using System.Reflection;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Web.Components.Pages;
using FluentAssertions;

namespace ddpc.DartSuite.Web.Tests;

public sealed class TournamentsRoundSaveTargetTests
{
    [Fact]
    public void ResolveRoundSaveTargets_UsesAllRoundsFromSelectedGroup()
    {
        var sut = new Tournaments();
        SetPrivateField(sut, "newRoundPhase", "Knockout");
        SetPrivateField(sut, "newRoundNumber", 99);
        SetPrivateField(sut, "detailRoundGroup", new List<TournamentRoundDto>
        {
            CreateRound("Knockout", 2),
            CreateRound("Knockout", 1),
            CreateRound("Group", 3)
        });

        var targets = InvokeResolveRoundSaveTargets(sut);

        targets.Should().BeEquivalentTo(
        [
            ("Group", 3),
            ("Knockout", 1),
            ("Knockout", 2)
        ]);
    }

    [Fact]
    public void ResolveRoundSaveTargets_FallsBackToCurrentRoundSelection()
    {
        var sut = new Tournaments();
        SetPrivateField(sut, "newRoundPhase", "Group");
        SetPrivateField(sut, "newRoundNumber", 4);
        SetPrivateField(sut, "detailRoundGroup", new List<TournamentRoundDto>());

        var targets = InvokeResolveRoundSaveTargets(sut);

        targets.Should().Equal([("Group", 4)]);
    }

    private static TournamentRoundDto CreateRound(string phase, int roundNumber) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        phase,
        roundNumber,
        501,
        "Straight",
        "Double",
        "Legs",
        3,
        null,
        50,
        "25/50",
        "Normal",
        0,
        0,
        0,
        "Dynamic",
        null);

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Das Feld '{fieldName}' muss existieren.");
        field!.SetValue(target, value);
    }

    private static List<(string Phase, int RoundNumber)> InvokeResolveRoundSaveTargets(Tournaments sut)
    {
        var method = sut.GetType().GetMethod("ResolveRoundSaveTargets", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Die Methode ResolveRoundSaveTargets muss existieren.");

        var result = method!.Invoke(sut, null) as System.Collections.IEnumerable;
        result.Should().NotBeNull();

        var targets = new List<(string Phase, int RoundNumber)>();
        foreach (var item in result!)
        {
            var itemType = item!.GetType();
            var phase = itemType.GetProperty("Phase")!.GetValue(item)?.ToString();
            var roundNumberObj = itemType.GetProperty("RoundNumber")!.GetValue(item);
            targets.Add((phase ?? string.Empty, Convert.ToInt32(roundNumberObj)));
        }

        return targets;
    }
}
