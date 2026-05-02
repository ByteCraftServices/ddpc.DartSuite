using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Web.Services;
using FluentAssertions;

namespace ddpc.DartSuite.Web.Tests;

public sealed class AppStateServiceTests
{
    private static TournamentDto MakeTournament(string name = "Test") => new(
        Id: Guid.NewGuid(),
        Name: name,
        OrganizerAccount: "admin",
        Status: "Erstellt",
        StartDate: DateOnly.FromDateTime(DateTime.Today),
        EndDate: DateOnly.FromDateTime(DateTime.Today),
        StartTime: null,
        Mode: "Knockout",
        Variant: "OnSite",
        TeamplayEnabled: false,
        IsLocked: false,
        AreGameModesLocked: false,
        JoinCode: null,
        ParticipantCount: 0,
        GroupCount: 2,
        PlayoffAdvancers: 2,
        KnockoutsPerRound: 1,
        MatchesPerOpponent: 1,
        GroupMode: "RoundRobin",
        GroupDrawMode: "Random",
        PlanningVariant: "RoundByRound",
        GroupOrderMode: "ReverseEachRound",
        ThirdPlaceMatch: false,
        PlayersPerTeam: 1,
        WinPoints: 2,
        LegFactor: 1);

    private static AutodartsProfileDto MakeProfile() => new("1", "TestUser", "DE", "test@test.com");

    [Fact]
    public void SetSelectedTournament_FiresOnChange()
    {
        var sut = new AppStateService();
        var fired = false;
        sut.OnChange += () => fired = true;

        sut.SetSelectedTournament(MakeTournament());

        fired.Should().BeTrue();
        sut.SelectedTournament.Should().NotBeNull();
    }

    [Fact]
    public void SetSelectedTournamentSilent_DoesNotFireOnChange()
    {
        var sut = new AppStateService();
        var fired = false;
        sut.OnChange += () => fired = true;

        sut.SetSelectedTournamentSilent(MakeTournament());

        fired.Should().BeFalse();
        sut.SelectedTournament.Should().NotBeNull();
    }

    [Fact]
    public void SetAutodartsProfile_FiresOnChange_And_SetsProperties()
    {
        var sut = new AppStateService();
        var fired = false;
        sut.OnChange += () => fired = true;
        var profile = MakeProfile();

        sut.SetAutodartsProfile(profile, true);

        fired.Should().BeTrue();
        sut.AutodartsProfile.Should().BeSameAs(profile);
        sut.IsAutodartsConnected.Should().BeTrue();
    }

    [Fact]
    public void SetAutodartsProfileSilent_DoesNotFireOnChange()
    {
        var sut = new AppStateService();
        var fired = false;
        sut.OnChange += () => fired = true;

        sut.SetAutodartsProfileSilent(MakeProfile(), true);

        fired.Should().BeFalse();
        sut.AutodartsProfile.Should().NotBeNull();
        sut.IsAutodartsConnected.Should().BeTrue();
    }

    [Fact]
    public void SetSelectedTournament_Null_ClearsTournament()
    {
        var sut = new AppStateService();
        sut.SetSelectedTournamentSilent(MakeTournament());

        sut.SetSelectedTournament(null);

        sut.SelectedTournament.Should().BeNull();
    }

    [Fact]
    public void SetAutodartsProfile_Null_ClearsProfile()
    {
        var sut = new AppStateService();
        sut.SetAutodartsProfileSilent(MakeProfile(), true);

        sut.SetAutodartsProfile(null, false);

        sut.AutodartsProfile.Should().BeNull();
        sut.IsAutodartsConnected.Should().BeFalse();
    }

    [Fact]
    public void OnChange_MultipleSubscribers_AllNotified()
    {
        var sut = new AppStateService();
        var count = 0;
        sut.OnChange += () => count++;
        sut.OnChange += () => count++;

        sut.SetSelectedTournament(MakeTournament());

        count.Should().Be(2);
    }

    [Fact]
    public void UnsubscribedHandler_NotFired()
    {
        var sut = new AppStateService();
        var fired = false;
        void Handler() => fired = true;
        sut.OnChange += Handler;
        sut.OnChange -= Handler;

        sut.SetSelectedTournament(MakeTournament());

        fired.Should().BeFalse();
    }

    // ─── LoginSource tracking ───────────────────────────────────────────────

    [Fact]
    public void SetAutodartsProfile_ManualSource_SetsLoginSourceManual()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.Manual);

        sut.LoginSource.Should().Be(AutodartsLoginSource.Manual);
    }

    [Fact]
    public void SetAutodartsProfile_AutoRestoreSource_SetsLoginSourceAutoRestore()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.AutoRestore);

        sut.LoginSource.Should().Be(AutodartsLoginSource.AutoRestore);
    }

    [Fact]
    public void SetAutodartsProfile_Disconnected_ResetsLoginSourceToNone()
    {
        var sut = new AppStateService();
        sut.SetAutodartsProfileSilent(MakeProfile(), true, AutodartsLoginSource.Manual);

        sut.SetAutodartsProfile(null, false, AutodartsLoginSource.None);

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void SetAutodartsProfileSilent_AutoRestoreSource_SetsLoginSourceWithoutFiringOnChange()
    {
        var sut = new AppStateService();
        var fired = false;
        sut.OnChange += () => fired = true;

        sut.SetAutodartsProfileSilent(MakeProfile(), true, AutodartsLoginSource.AutoRestore);

        fired.Should().BeFalse();
        sut.LoginSource.Should().Be(AutodartsLoginSource.AutoRestore);
    }

    [Fact]
    public void SetAutodartsProfile_WhenDisconnected_ResetsLoginSourceToNone()
    {
        var sut = new AppStateService();
        sut.SetAutodartsProfileSilent(MakeProfile(), true, AutodartsLoginSource.Manual);

        // Passing AutoRestore but disconnected — source must still become None
        sut.SetAutodartsProfile(null, false, AutodartsLoginSource.AutoRestore);

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void LoginSource_DefaultsToNone()
    {
        var sut = new AppStateService();

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void SetAutodartsProfile_DefaultSourceIsManual_WhenNoSourceProvided()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(MakeProfile(), true);

        sut.LoginSource.Should().Be(AutodartsLoginSource.Manual);
    }
}
