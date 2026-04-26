using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

/// <summary>
/// Unit tests for Virtual Board Management (#44).
/// Tests creation, listing, owner management, and deletion of virtual boards.
/// </summary>
public sealed class VirtualBoardManagementTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateVirtualBoard_ShouldCreateBoardWithIsVirtualTrue()
    {
        await using var dbContext = CreateDbContext();
        var service = new BoardManagementService(dbContext);

        var board = await service.CreateVirtualBoardAsync(
            new CreateVirtualBoardRequest("Virtual Board 1", "admin@example.com"));

        board.IsVirtual.Should().BeTrue();
        board.Name.Should().Be("Virtual Board 1");
        board.OwnerAccountName.Should().Be("admin@example.com");
        board.Status.Should().Be("Running");
        board.ConnectionState.Should().Be("Online");
        board.ExternalBoardId.Should().Be(Guid.Empty.ToString());
    }

    [Fact]
    public async Task ConvertExistingBoardToVirtual_ShouldMarkBoardAsVirtualAndSetOnlineState()
    {
        await using var dbContext = CreateDbContext();
        var existing = new Board
        {
            ExternalBoardId = "physical-42",
            Name = "Physical 42",
            IsVirtual = false,
            Status = BoardStatus.Offline,
            ConnectionState = ConnectionState.Offline,
            LocalIpAddress = "192.168.1.5",
            BoardManagerUrl = "http://192.168.1.5"
        };
        dbContext.Boards.Add(existing);
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var converted = await service.ConvertBoardToVirtualAsync(existing.Id, "owner@example.com");

        converted.Should().NotBeNull();
        converted!.IsVirtual.Should().BeTrue();
        converted.OwnerAccountName.Should().Be("owner@example.com");
        converted.Status.Should().Be("Running");
        converted.ConnectionState.Should().Be("Online");
        converted.LocalIpAddress.Should().BeNull();
        converted.BoardManagerUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetVirtualBoards_ShouldReturnOnlyVirtualBoards()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Boards.AddRange(
            new Board { ExternalBoardId = "physical-1", Name = "Physical", IsVirtual = false },
            new Board { ExternalBoardId = "virtual-1", Name = "Virtual 1", IsVirtual = true },
            new Board { ExternalBoardId = "virtual-2", Name = "Virtual 2", IsVirtual = true }
        );
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var virtualBoards = await service.GetVirtualBoardsAsync();

        virtualBoards.Should().HaveCount(2);
        virtualBoards.Select(x => x.Name).Should().BeEquivalentTo(["Virtual 1", "Virtual 2"]);
        virtualBoards.All(x => x.IsVirtual).Should().BeTrue();
    }

    [Fact]
    public async Task ChangeVirtualBoardOwner_ShouldUpdateOwnerAccountName()
    {
        await using var dbContext = CreateDbContext();
        var board = new Board
        {
            ExternalBoardId = "virtual-1",
            Name = "Virtual Test",
            IsVirtual = true,
            OwnerAccountName = "oldowner@example.com"
        };
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var updated = await service.ChangeVirtualBoardOwnerAsync(board.Id, "newowner@example.com");

        updated.Should().NotBeNull();
        updated!.OwnerAccountName.Should().Be("newowner@example.com");

        var reloaded = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == board.Id);
        reloaded!.OwnerAccountName.Should().Be("newowner@example.com");
    }

    [Fact]
    public async Task ChangeVirtualBoardOwner_WithNullOwner_ShouldClearOwner()
    {
        await using var dbContext = CreateDbContext();
        var board = new Board
        {
            ExternalBoardId = "virtual-1",
            Name = "Virtual Test",
            IsVirtual = true,
            OwnerAccountName = "owner@example.com"
        };
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var updated = await service.ChangeVirtualBoardOwnerAsync(board.Id, null);

        updated.Should().NotBeNull();
        updated!.OwnerAccountName.Should().BeNull();
    }

    [Fact]
    public async Task DeleteVirtualBoard_WithActiveMatch_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();
        var board = new Board
        {
            ExternalBoardId = "virtual-1",
            Name = "Virtual Test",
            IsVirtual = true
        };
        dbContext.Boards.Add(board);
        
        var participant1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "Player 1" };
        var participant2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "Player 2" };
        dbContext.Participants.AddRange(participant1, participant2);
        
        dbContext.Matches.Add(new Match
        {
            BoardId = board.Id,
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = participant1.Id,
            AwayParticipantId = participant2.Id,
            Status = MatchStatus.Aktiv,
            StartedUtc = DateTimeOffset.UtcNow,
            FinishedUtc = null  // Still running
        });
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.DeleteBoardAsync(board.Id));
        ex.Message.Should().Contain("Board hat laufende Matches");
    }

    [Fact]
    public async Task DeleteVirtualBoard_WithFutureMatches_ShouldReassignToOtherBoard()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();
        var boardToDelete = new Board
        {
            ExternalBoardId = "virtual-1",
            Name = "Delete Me",
            IsVirtual = true
        };
        var otherBoard = new Board
        {
            ExternalBoardId = "virtual-2",
            Name = "Fallback",
            IsVirtual = true
        };
        dbContext.Boards.AddRange(boardToDelete, otherBoard);

        var p1 = new Participant { TournamentId = tournamentId, AccountName = "p1", DisplayName = "P1" };
        var p2 = new Participant { TournamentId = tournamentId, AccountName = "p2", DisplayName = "P2" };
        dbContext.Participants.AddRange(p1, p2);

        var futureMatch = new Match
        {
            BoardId = boardToDelete.Id,
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = p1.Id,
            AwayParticipantId = p2.Id,
            Status = MatchStatus.Geplant,
            StartedUtc = null,
            FinishedUtc = null
        };
        dbContext.Matches.Add(futureMatch);
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var result = await service.DeleteBoardAsync(boardToDelete.Id);

        result.Should().BeTrue();
        var reloaded = await dbContext.Matches.FirstAsync(x => x.Id == futureMatch.Id);
        reloaded.BoardId.Should().Be(otherBoard.Id);
    }

    [Fact]
    public async Task VirtualBoardAssignedToTournament_ShouldBeListedInTournament()
    {
        await using var dbContext = CreateDbContext();
        var tournamentId = Guid.NewGuid();
        
        var virtualBoard = new Board
        {
            ExternalBoardId = "virtual-1",
            Name = "Virtual Board",
            IsVirtual = true,
            TournamentId = tournamentId
        };
        dbContext.Boards.Add(virtualBoard);
        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var boards = await service.GetBoardsByTournamentAsync(tournamentId);

        boards.Should().ContainSingle();
        boards.First().IsVirtual.Should().BeTrue();
        boards.First().Name.Should().Be("Virtual Board");
    }
}
