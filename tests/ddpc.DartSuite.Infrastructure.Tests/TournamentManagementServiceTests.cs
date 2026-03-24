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
}