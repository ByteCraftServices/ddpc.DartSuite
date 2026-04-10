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
}