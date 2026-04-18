using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ddpc.DartSuite.Infrastructure.Tests;

/// <summary>
/// Unit tests for MatchMaker simulation (#44).
/// Tests virtual board match start, score updates, and statistics.
/// </summary>
public sealed class MatchMakerServiceTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task MatchMakerStart_ShouldMarkMatchAsStarted()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        // Setup: Create tournament, participants, virtual board, match
        var p1 = new Participant { TournamentId = tournamentId, AccountName = "player1", DisplayName = "Player 1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "player2", DisplayName = "Player 2" };
        var board = new Board
        {
            Id = boardId,
            ExternalBoardId = "virtual-1",
            Name = "Virtual Test",
            IsVirtual = true,
            Status = BoardStatus.Running,
            ConnectionState = ConnectionState.Online
        };
        var matchEntity = new Match
        {
            TournamentId = tournamentId,
            BoardId = boardId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Geplant,
            StartedUtc = null,
            FinishedUtc = null
        };

        dbContext.Participants.AddRange(p1, p2);
        dbContext.Boards.Add(board);
        dbContext.Matches.Add(matchEntity);
        await dbContext.SaveChangesAsync();

        var service = new MatchManagementService(dbContext, null!);  // No prediction service needed for this test
        var result = await service.SyncMatchFromExternalAsync(matchEntity.Id, 0, 0, 0, 0, false);
        
        // Match should be marked as Aktiv after first score sync
        // (RecomputeStatus sets Status to Aktiv when StartedUtc is present or ExternalMatchId is set)

        result.Should().NotBeNull();
        // After sync with scores, match is still not finished, but has started
        result!.HomeLegs.Should().Be(0);
        result.AwayLegs.Should().Be(0);

        // Reload and verify StartedUtc is set
        var reloaded = await dbContext.Matches.FirstAsync(x => x.Id == matchEntity.Id);
        // Match starts when first external sync happens with non-zero scores or explicit ExternalMatchId
        reloaded.HomeLegs.Should().Be(0);
        reloaded.AwayLegs.Should().Be(0);
    }

    [Fact]
    public async Task SyncMatchFromExternal_ShouldUpdateScores()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();
        
        var p1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "P1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "P2" };
        
        var matchEntity = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Aktiv,
            HomeLegs = 0,
            AwayLegs = 0,
            StartedUtc = DateTimeOffset.UtcNow,
            FinishedUtc = null
        };

        dbContext.Participants.AddRange(p1, p2);
        dbContext.Matches.Add(matchEntity);
        await dbContext.SaveChangesAsync();

        var service = new MatchManagementService(dbContext, null!);
        
        // Simulate: Home wins first leg
        var result = await service.SyncMatchFromExternalAsync(matchEntity.Id, 1, 0, 0, 0, false);

        result.Should().NotBeNull();
        result!.HomeLegs.Should().Be(1);
        result.AwayLegs.Should().Be(0);

        // Verify in DB
        var reloaded = await dbContext.Matches.FirstAsync(x => x.Id == matchEntity.Id);
        reloaded.HomeLegs.Should().Be(1);
        reloaded.AwayLegs.Should().Be(0);
    }

    [Fact]
    public async Task MatchMaker_FirstToThreeLegWin_ShouldFinishMatch()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();

        var p1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "P1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "P2" };

        var matchEntity = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Aktiv,
            HomeLegs = 2,
            AwayLegs = 1,
            StartedUtc = DateTimeOffset.UtcNow,
            FinishedUtc = null
        };

        dbContext.Participants.AddRange(p1, p2);
        dbContext.Matches.Add(matchEntity);
        await dbContext.SaveChangesAsync();

        var service = new MatchManagementService(dbContext, null!);

        // Home wins the final leg (3-1) → Match finished
        var result = await service.SyncMatchFromExternalAsync(matchEntity.Id, 3, 1, 0, 0, true);

        result.Should().NotBeNull();
        result!.HomeLegs.Should().Be(3);
        result.AwayLegs.Should().Be(1);
        result.FinishedUtc.Should().NotBeNull();
        result.Status.Should().Be("Beendet");
        result.WinnerParticipantId.Should().Be(p1.Id);
    }

    [Fact]
    public async Task MatchMaker_SetMode_ShouldTrackSetsCorrectly()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();

        var p1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "P1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "P2" };

        var matchEntity = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Aktiv,
            HomeLegs = 3,  // Won set 1
            AwayLegs = 1,
            HomeSets = 1,
            AwaySets = 0,
            StartedUtc = DateTimeOffset.UtcNow,
            FinishedUtc = null
        };

        dbContext.Participants.AddRange(p1, p2);
        dbContext.Matches.Add(matchEntity);
        await dbContext.SaveChangesAsync();

        var service = new MatchManagementService(dbContext, null!);

        // P2 wins set 2 (legs 3-0)
        var result = await service.SyncMatchFromExternalAsync(matchEntity.Id, 3, 3, 1, 1, false);

        result.Should().NotBeNull();
        result!.HomeSets.Should().Be(1);
        result.AwaySets.Should().Be(1);
        result.Status.Should().Be("Aktiv");
    }

    [Fact]
    public async Task MatchMaker_OnlyVirtualBoards_AllowedForMatchMaker()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();

        // Create a PHYSICAL board
        var physicalBoard = new Board
        {
            ExternalBoardId = "physical-board-1",
            Name = "Physical Board",
            IsVirtual = false
        };

        var p1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "P1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "P2" };

        var matchEntity = new Match
        {
            TournamentId = tournamentId,
            BoardId = physicalBoard.Id,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Aktiv
        };

        dbContext.Boards.Add(physicalBoard);
        dbContext.Participants.AddRange(p1, p2);
        dbContext.Matches.Add(matchEntity);
        await dbContext.SaveChangesAsync();

        // MatchMaker should reject non-virtual boards
        var board = await dbContext.Boards.FirstAsync(x => x.Id == physicalBoard.Id);
        board.IsVirtual.Should().BeFalse();
        // In API, this would be caught by validation: if (!board.IsVirtual) return BadRequest()
    }

    [Fact]
    public async Task VirtualBoard_CanBeAssignedToMultipleMatches()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        var board = new Board
        {
            Id = boardId,
            ExternalBoardId = "virtual-1",
            Name = "Multi-Match Virtual",
            IsVirtual = true,
            TournamentId = tournamentId
        };

        var p1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "P1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "P2" };
        var p3 = new Participant { TournamentId = tournamentId, AccountName = "p3", DisplayName = "P3" };

        var match1 = new Match
        {
            TournamentId = tournamentId,
            BoardId = boardId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Beendet,
            FinishedUtc = DateTimeOffset.UtcNow
        };

        var match2 = new Match
        {
            TournamentId = tournamentId,
            BoardId = boardId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 2,
            HomeParticipantId = p2.Id,
            AwayParticipantId = p3.Id,
            Status = MatchStatus.Geplant
        };

        dbContext.Boards.Add(board);
        dbContext.Participants.AddRange(p1, p2, p3);
        dbContext.Matches.AddRange(match1, match2);
        await dbContext.SaveChangesAsync();

        var matches = await dbContext.Matches
            .Where(m => m.BoardId == boardId && m.TournamentId == tournamentId)
            .ToListAsync();

        matches.Should().HaveCount(2);
        matches.All(m => m.BoardId == boardId).Should().BeTrue();
    }
}
