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
}