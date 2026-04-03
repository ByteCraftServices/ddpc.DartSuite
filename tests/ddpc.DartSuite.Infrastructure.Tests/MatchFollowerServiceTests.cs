using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class MatchFollowerServiceTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(MatchManagementService service, Guid matchId)> SetupWithMatchAsync()
    {
        var db = CreateDbContext();
        var match = new Match
        {
            TournamentId = Guid.NewGuid(),
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid()
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        var service = new MatchManagementService(db, new MatchPredictionService());
        return (service, match.Id);
    }

    [Fact]
    public async Task FollowMatch_ShouldPersist()
    {
        var (service, matchId) = await SetupWithMatchAsync();

        var result = await service.FollowMatchAsync(matchId, "user-a");

        result.MatchId.Should().Be(matchId);
        result.UserAccountName.Should().Be("user-a");
    }

    [Fact]
    public async Task FollowMatch_DuplicateReturnsExisting()
    {
        var (service, matchId) = await SetupWithMatchAsync();

        var first = await service.FollowMatchAsync(matchId, "user-a");
        var second = await service.FollowMatchAsync(matchId, "user-a");

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetMatchFollowers_ShouldReturnFollowers()
    {
        var (service, matchId) = await SetupWithMatchAsync();

        await service.FollowMatchAsync(matchId, "user-a");
        await service.FollowMatchAsync(matchId, "user-b");
        var followers = await service.GetMatchFollowersAsync(matchId);

        followers.Should().HaveCount(2);
    }

    [Fact]
    public async Task UnfollowMatch_ShouldRemove()
    {
        var (service, matchId) = await SetupWithMatchAsync();

        await service.FollowMatchAsync(matchId, "user-a");
        var result = await service.UnfollowMatchAsync(matchId, "user-a");
        var followers = await service.GetMatchFollowersAsync(matchId);

        result.Should().BeTrue();
        followers.Should().BeEmpty();
    }

    [Fact]
    public async Task UnfollowMatch_NonExistent_ReturnsFalse()
    {
        var (service, matchId) = await SetupWithMatchAsync();

        var result = await service.UnfollowMatchAsync(matchId, "user-nonexistent");

        result.Should().BeFalse();
    }
}
