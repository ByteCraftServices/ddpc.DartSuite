using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class MatchBoardScopeConsistencyTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AssignBoardAsync_ShouldRejectBoardFromDifferentTournament()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentA = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Cup A",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today)
        };
        var tournamentB = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Cup B",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today)
        };

        var boardB = new Board { Id = Guid.NewGuid(), Name = "Board B", ExternalBoardId = "b-b", TournamentId = tournamentB.Id };
        var matchA = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentA.Id,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid()
        };

        db.Tournaments.AddRange(tournamentA, tournamentB);
        db.Boards.Add(boardB);
        db.Matches.Add(matchA);
        await db.SaveChangesAsync();

        var action = () => service.AssignBoardAsync(matchA.Id, boardB.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anderen Turnier*");
    }

    [Fact]
    public async Task UpdateMatchScheduleAsync_ShouldRejectBoardFromDifferentTournament()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentA = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Cup A",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today)
        };
        var tournamentB = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Cup B",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today)
        };

        var boardB = new Board { Id = Guid.NewGuid(), Name = "Board B", ExternalBoardId = "b-b", TournamentId = tournamentB.Id };
        var matchA = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentA.Id,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid()
        };

        db.Tournaments.AddRange(tournamentA, tournamentB);
        db.Boards.Add(boardB);
        db.Matches.Add(matchA);
        await db.SaveChangesAsync();

        var action = () => service.UpdateMatchScheduleAsync(matchA.Id, DateTimeOffset.UtcNow, lockTime: false, boardId: boardB.Id, lockBoard: false);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anderen Turnier*");
    }

    [Fact]
    public async Task GenerateScheduleAsync_ShouldIgnoreFixedBoardFromDifferentTournament()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentA = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Cup A",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(18, 0)
        };
        var tournamentB = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Cup B",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(18, 0)
        };

        var boardA = new Board { Id = Guid.NewGuid(), Name = "Board A", ExternalBoardId = "b-a", TournamentId = tournamentA.Id };
        var boardForeign = new Board { Id = Guid.NewGuid(), Name = "Board B", ExternalBoardId = "b-b", TournamentId = tournamentB.Id };

        var round = new TournamentRound
        {
            TournamentId = tournamentA.Id,
            Phase = MatchPhase.Knockout,
            RoundNumber = 1,
            LegDurationSeconds = 300,
            BoardAssignment = BoardAssignmentMode.Fixed,
            FixedBoardId = boardForeign.Id
        };

        var matchA = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentA.Id,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid()
        };

        db.Tournaments.AddRange(tournamentA, tournamentB);
        db.Boards.AddRange(boardA, boardForeign);
        db.TournamentRounds.Add(round);
        db.Matches.Add(matchA);
        await db.SaveChangesAsync();

        var scheduled = await service.GenerateScheduleAsync(tournamentA.Id);

        scheduled.Should().ContainSingle();
        scheduled[0].BoardId.Should().Be(boardA.Id);
    }
}
