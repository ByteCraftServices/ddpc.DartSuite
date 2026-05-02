using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class TournamentManagementServiceTests
{
    [Fact]
    public async Task AddParticipant_ShouldPersistParticipant()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite"));

        await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 1",
            "player1",
            true,
            false,
            1));

        var participants = await service.GetParticipantsAsync(tournament.Id);
        participants.Should().ContainSingle();
    }

    [Fact]
    public async Task AddParticipant_ShouldRejectTeamMemberType()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite"));

        var act = async () => await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Team Ghost",
            "team-ghost",
            false,
            false,
            1,
            "TeamMember"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddParticipant_ShouldRejectDuplicateDisplayName()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite"));

        await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id, "Player One", "player1", false, false, 1));

        // Same DisplayName, different AccountName → should be rejected
        var act = async () => await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id, "Player One", "player1-alt", false, false, 2));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Anzeigenamen*");
    }

    [Fact]
    public async Task AddParticipant_ShouldRejectDuplicateDisplayName_CaseInsensitive()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite"));

        await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id, "Anna", "anna1", false, false, 1));

        var act = async () => await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id, "ANNA", "anna2", false, false, 2));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Anzeigenamen*");
    }

    [Fact]
    public async Task AddParticipant_ShouldAllowSameDisplayNameInDifferentTournaments()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var t1 = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Turnier A", "manager", DateOnly.FromDateTime(DateTime.Today), null, false, "Knockout", "OnSite"));
        var t2 = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Turnier B", "manager", DateOnly.FromDateTime(DateTime.Today), null, false, "Knockout", "OnSite"));

        await service.AddParticipantAsync(new AddParticipantRequest(t1.Id, "Max", "max1", false, false, 1));

        // Same DisplayName in a different tournament → must succeed
        var act = async () => await service.AddParticipantAsync(
            new AddParticipantRequest(t2.Id, "Max", "max1", false, false, 1));

        await act.Should().NotThrowAsync();
    }


    [Fact]
    public async Task UpdateParticipant_ShouldRejectPlayerToTeamMemberConversion()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite"));

        var participant = await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 1",
            "player1",
            true,
            false,
            1));

        var act = async () => await service.UpdateParticipantAsync(new UpdateParticipantRequest(
            tournament.Id,
            participant.Id,
            participant.DisplayName,
            participant.AccountName,
            participant.IsAutodartsAccount,
            participant.IsManager,
            participant.Seed,
            participant.SeedPot,
            participant.GroupNumber,
            "TeamMember"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateParticipant_ShouldAllowTeamMemberUpdateWithoutTypeChange()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            true,
            "Knockout",
            "OnSite"));

        var player = await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 1",
            "player1",
            true,
            false,
            1));

        var createdTeam = await service.CreateTeamAsync(new CreateTeamRequest(
            tournament.Id,
            "Team One",
            [player.Id]));

        var teamMember = (await service.GetParticipantsAsync(tournament.Id))
            .Single(p => p.TeamId == createdTeam.Id && string.Equals(p.Type, "TeamMember", StringComparison.OrdinalIgnoreCase));

        var updated = await service.UpdateParticipantAsync(new UpdateParticipantRequest(
            tournament.Id,
            teamMember.Id,
            teamMember.DisplayName,
            teamMember.AccountName,
            teamMember.IsAutodartsAccount,
            teamMember.IsManager,
            3,
            teamMember.SeedPot,
            teamMember.GroupNumber,
            teamMember.Type));

        updated.Should().NotBeNull();
        updated!.Type.Should().Be("TeamMember");
        updated.Seed.Should().Be(3);
    }

    [Fact]
    public async Task UpdateTournament_TeamplayEnabled_ShouldResetNonTeamMemberSeeds()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite"));

        var player = await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 1",
            "player1",
            true,
            false,
            7));

        var updatedTournament = await service.UpdateTournamentAsync(new UpdateTournamentRequest(
            tournament.Id,
            tournament.Name,
            tournament.OrganizerAccount,
            tournament.StartDate,
            tournament.EndDate,
            true,
            tournament.Mode,
            tournament.Variant,
            null,
            tournament.GroupCount,
            tournament.PlayoffAdvancers,
            tournament.KnockoutsPerRound,
            tournament.MatchesPerOpponent,
            tournament.GroupMode,
            tournament.GroupDrawMode,
            tournament.PlanningVariant,
            tournament.GroupOrderMode,
            tournament.ThirdPlaceMatch,
            2,
            tournament.WinPoints,
            tournament.LegFactor,
            tournament.AreGameModesLocked,
            tournament.IsRegistrationOpen,
            tournament.RegistrationStartUtc,
            tournament.RegistrationEndUtc,
            tournament.DiscordWebhookUrl,
            tournament.DiscordWebhookDisplayText,
            true,
            10));

        updatedTournament.Should().NotBeNull();

        var participants = await service.GetParticipantsAsync(tournament.Id);
        var updatedPlayer = participants.Single(p => p.Id == player.Id);
        updatedPlayer.Seed.Should().Be(0);
        updatedPlayer.SeedPot.Should().Be(0);
    }

    [Fact]
    public async Task UpdateTournament_TeamplayEnabled_ShouldClampSeedTopCountToTeamMemberCount()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            true,
            "Knockout",
            "OnSite"));

        var player = await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 1",
            "player1",
            true,
            false,
            1));

        await service.CreateTeamAsync(new CreateTeamRequest(
            tournament.Id,
            "Team One",
            [player.Id]));

        var updatedTournament = await service.UpdateTournamentAsync(new UpdateTournamentRequest(
            tournament.Id,
            tournament.Name,
            tournament.OrganizerAccount,
            tournament.StartDate,
            tournament.EndDate,
            true,
            tournament.Mode,
            tournament.Variant,
            null,
            tournament.GroupCount,
            tournament.PlayoffAdvancers,
            tournament.KnockoutsPerRound,
            tournament.MatchesPerOpponent,
            tournament.GroupMode,
            tournament.GroupDrawMode,
            tournament.PlanningVariant,
            tournament.GroupOrderMode,
            tournament.ThirdPlaceMatch,
            1,
            tournament.WinPoints,
            tournament.LegFactor,
            tournament.AreGameModesLocked,
            tournament.IsRegistrationOpen,
            tournament.RegistrationStartUtc,
            tournament.RegistrationEndUtc,
            tournament.DiscordWebhookUrl,
            tournament.DiscordWebhookDisplayText,
            true,
            99));

        updatedTournament.Should().NotBeNull();
        updatedTournament!.SeedTopCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveTeams_TeamplayEnabled_ShouldAllowPartialAssignmentsForAutosave()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            true,
            "Knockout",
            "OnSite"));

        await service.UpdateTournamentAsync(new UpdateTournamentRequest(
            tournament.Id,
            tournament.Name,
            tournament.OrganizerAccount,
            tournament.StartDate,
            tournament.EndDate,
            true,
            tournament.Mode,
            tournament.Variant,
            null,
            tournament.GroupCount,
            tournament.PlayoffAdvancers,
            tournament.KnockoutsPerRound,
            tournament.MatchesPerOpponent,
            tournament.GroupMode,
            tournament.GroupDrawMode,
            tournament.PlanningVariant,
            tournament.GroupOrderMode,
            tournament.ThirdPlaceMatch,
            2,
            tournament.WinPoints,
            tournament.LegFactor,
            tournament.AreGameModesLocked,
            tournament.IsRegistrationOpen,
            tournament.RegistrationStartUtc,
            tournament.RegistrationEndUtc,
            tournament.DiscordWebhookUrl,
            tournament.DiscordWebhookDisplayText,
            tournament.SeedingEnabled,
            tournament.SeedTopCount));

        var p1 = await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 1",
            "player1",
            true,
            false,
            1));

        await service.AddParticipantAsync(new AddParticipantRequest(
            tournament.Id,
            "Player 2",
            "player2",
            true,
            false,
            2));

        var result = await service.SaveTeamsAsync(new SaveTeamsRequest(
            tournament.Id,
            [new SaveTeamRequest(null, "Team Alpha", [p1.Id])]
        ));

        result.Should().HaveCount(1);
        result[0].Members.Should().ContainSingle(m => m.Id == p1.Id);
    }

    [Fact]
    public async Task UpdateTournament_TeamplayDisabled_ShouldCleanupTeamsAndTeamMembers()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            true,
            "Knockout",
            "OnSite"));

        tournament = await service.UpdateTournamentAsync(new UpdateTournamentRequest(
            tournament.Id, tournament.Name, tournament.OrganizerAccount, tournament.StartDate,
            tournament.EndDate, true, tournament.Mode, tournament.Variant, null,
            tournament.GroupCount, tournament.PlayoffAdvancers, tournament.KnockoutsPerRound,
            tournament.MatchesPerOpponent, tournament.GroupMode, tournament.GroupDrawMode,
            tournament.PlanningVariant, tournament.GroupOrderMode, tournament.ThirdPlaceMatch,
            2, tournament.WinPoints, tournament.LegFactor, tournament.AreGameModesLocked,
            tournament.IsRegistrationOpen, tournament.RegistrationStartUtc, tournament.RegistrationEndUtc,
            tournament.DiscordWebhookUrl, tournament.DiscordWebhookDisplayText,
            tournament.SeedingEnabled, tournament.SeedTopCount)) ?? tournament;

        var p1 = await service.AddParticipantAsync(new AddParticipantRequest(tournament.Id, "Player 1", "p1", false, false, 1));
        var p2 = await service.AddParticipantAsync(new AddParticipantRequest(tournament.Id, "Player 2", "p2", false, false, 2));

        await service.SaveTeamsAsync(new SaveTeamsRequest(
            tournament.Id,
            [new SaveTeamRequest(null, "Team A", [p1.Id, p2.Id])]));

        // Verify team was created
        var teamsBeforeDisable = await service.GetTeamsAsync(tournament.Id);
        teamsBeforeDisable.Should().HaveCount(1);

        // Disable teamplay
        await service.UpdateTournamentAsync(new UpdateTournamentRequest(
            tournament.Id, tournament.Name, tournament.OrganizerAccount, tournament.StartDate,
            tournament.EndDate, false, tournament.Mode, tournament.Variant, null,
            tournament.GroupCount, tournament.PlayoffAdvancers, tournament.KnockoutsPerRound,
            tournament.MatchesPerOpponent, tournament.GroupMode, tournament.GroupDrawMode,
            tournament.PlanningVariant, tournament.GroupOrderMode, tournament.ThirdPlaceMatch,
            2, tournament.WinPoints, tournament.LegFactor, tournament.AreGameModesLocked,
            tournament.IsRegistrationOpen, tournament.RegistrationStartUtc, tournament.RegistrationEndUtc,
            tournament.DiscordWebhookUrl, tournament.DiscordWebhookDisplayText,
            tournament.SeedingEnabled, tournament.SeedTopCount));

        var teamsAfterDisable = await service.GetTeamsAsync(tournament.Id);
        teamsAfterDisable.Should().BeEmpty();

        var participants = await service.GetParticipantsAsync(tournament.Id);
        participants.Should().NotContain(p => p.Type == "TeamMember");
        participants.Should().OnlyContain(p => p.TeamId == null);
    }

    [Fact]
    public async Task RemoveParticipant_WhenMemberOfTeam_ShouldCleanupTeam()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new TournamentManagementService(dbContext);

        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            true,
            "Knockout",
            "OnSite"));

        tournament = await service.UpdateTournamentAsync(new UpdateTournamentRequest(
            tournament.Id, tournament.Name, tournament.OrganizerAccount, tournament.StartDate,
            tournament.EndDate, true, tournament.Mode, tournament.Variant, null,
            tournament.GroupCount, tournament.PlayoffAdvancers, tournament.KnockoutsPerRound,
            tournament.MatchesPerOpponent, tournament.GroupMode, tournament.GroupDrawMode,
            tournament.PlanningVariant, tournament.GroupOrderMode, tournament.ThirdPlaceMatch,
            2, tournament.WinPoints, tournament.LegFactor, tournament.AreGameModesLocked,
            tournament.IsRegistrationOpen, tournament.RegistrationStartUtc, tournament.RegistrationEndUtc,
            tournament.DiscordWebhookUrl, tournament.DiscordWebhookDisplayText,
            tournament.SeedingEnabled, tournament.SeedTopCount)) ?? tournament;

        var p1 = await service.AddParticipantAsync(new AddParticipantRequest(tournament.Id, "Player 1", "p1", false, false, 1));
        var p2 = await service.AddParticipantAsync(new AddParticipantRequest(tournament.Id, "Player 2", "p2", false, false, 2));

        await service.SaveTeamsAsync(new SaveTeamsRequest(
            tournament.Id,
            [new SaveTeamRequest(null, "Team A", [p1.Id, p2.Id])]));

        // Remove one member of the team
        var removed = await service.RemoveParticipantAsync(tournament.Id, p1.Id);
        removed.Should().BeTrue();

        var teams = await service.GetTeamsAsync(tournament.Id);
        teams.Should().BeEmpty();

        var participants = await service.GetParticipantsAsync(tournament.Id);
        participants.Should().NotContain(p => p.Type == "TeamMember");
        participants.Should().NotContain(p => p.Id == p1.Id);
        // p2 should remain, unassigned
        participants.Should().Contain(p => p.Id == p2.Id && p.TeamId == null);
    }
}