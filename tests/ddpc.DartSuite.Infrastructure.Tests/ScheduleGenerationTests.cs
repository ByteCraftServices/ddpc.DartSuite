using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class ScheduleGenerationTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GenerateSchedule_ShouldUseAllTournamentBoards_AndDistributeEvenly()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(new Tournament
        {
            Id = tournamentId,
            Name = "Board Distribution Cup",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(18, 0)
        });

        var boards = new[]
        {
            new Board { Id = Guid.NewGuid(), ExternalBoardId = "b1", Name = "Board 1", TournamentId = tournamentId },
            new Board { Id = Guid.NewGuid(), ExternalBoardId = "b2", Name = "Board 2", TournamentId = tournamentId },
            new Board { Id = Guid.NewGuid(), ExternalBoardId = "b3", Name = "Board 3", TournamentId = tournamentId }
        };
        db.Boards.AddRange(boards);

        // Global board must not be used while tournament boards exist.
        db.Boards.Add(new Board { Id = Guid.NewGuid(), ExternalBoardId = "global", Name = "Global Board", TournamentId = null });

        var matches = Enumerable.Range(1, 9)
            .Select(i => new Match
            {
                TournamentId = tournamentId,
                Phase = MatchPhase.Knockout,
                Round = 1,
                MatchNumber = i,
                HomeParticipantId = Guid.NewGuid(),
                AwayParticipantId = Guid.NewGuid(),
                BoardId = boards[0].Id // Reproduce sticky first-board state before scheduling.
            })
            .ToList();

        db.Matches.AddRange(matches);
        await db.SaveChangesAsync();

        var scheduled = await service.GenerateScheduleAsync(tournamentId);

        var assignedBoards = scheduled
            .Where(m => m.Status != MatchStatus.WalkOver.ToString())
            .Select(m => m.BoardId)
            .OfType<Guid>()
            .ToList();

        assignedBoards.Should().NotBeEmpty();
        assignedBoards.Distinct().Should().BeEquivalentTo(boards.Select(b => b.Id));

        var boardCounts = assignedBoards
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var minCount = boardCounts.Values.Min();
        var maxCount = boardCounts.Values.Max();
        (maxCount - minCount).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task GenerateSchedule_ShouldRespectExistingPlannedStartOrderHints_ForGroupMatches()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var baseStart = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(18), TimeSpan.Zero);

        db.Tournaments.Add(new Tournament
        {
            Id = tournamentId,
            Name = "Order Hint Cup",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(18, 0)
        });

        db.Boards.Add(new Board { Id = boardId, ExternalBoardId = "b1", Name = "Board 1", TournamentId = tournamentId });

        var matchA = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            GroupNumber = 1,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid(),
            PlannedStartUtc = baseStart.AddMinutes(30),
            BoardId = boardId
        };
        var matchB = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            GroupNumber = 1,
            Round = 3,
            MatchNumber = 2,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid(),
            PlannedStartUtc = baseStart,
            BoardId = boardId
        };

        db.Matches.AddRange(matchA, matchB);
        await db.SaveChangesAsync();

        var scheduled = await service.GenerateScheduleAsync(tournamentId);

        var scheduledA = scheduled.Single(m => m.Id == matchA.Id);
        var scheduledB = scheduled.Single(m => m.Id == matchB.Id);
        scheduledB.PlannedStartUtc.Should().NotBeNull();
        scheduledA.PlannedStartUtc.Should().NotBeNull();
        scheduledB.PlannedStartUtc!.Value.Should().BeBefore(scheduledA.PlannedStartUtc!.Value);
    }

    [Fact]
    public async Task GenerateSchedule_ShouldKeepExistingBoardHint_WhenScheduledMatchIsMovedToAnotherQueue()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        var boardA = new Board { Id = Guid.NewGuid(), ExternalBoardId = "b1", Name = "Board 1", TournamentId = tournamentId };
        var boardB = new Board { Id = Guid.NewGuid(), ExternalBoardId = "b2", Name = "Board 2", TournamentId = tournamentId };
        var baseStart = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(18), TimeSpan.Zero);

        db.Tournaments.Add(new Tournament
        {
            Id = tournamentId,
            Name = "Queue Move Cup",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(18, 0)
        });
        db.Boards.AddRange(boardA, boardB);

        var movedMatch = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid(),
            PlannedStartUtc = baseStart,
            BoardId = boardB.Id
        };
        var otherMatch = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 2,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid(),
            PlannedStartUtc = baseStart.AddMinutes(30),
            BoardId = boardA.Id
        };

        db.Matches.AddRange(movedMatch, otherMatch);
        await db.SaveChangesAsync();

        var scheduled = await service.GenerateScheduleAsync(tournamentId);

        scheduled.Single(m => m.Id == movedMatch.Id).BoardId.Should().Be(boardB.Id);
    }
}
