using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class BoardManagementServiceTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateBoard_ShouldPersistBoard()
    {
        await using var dbContext = CreateDbContext();
        var service = new BoardManagementService(dbContext);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("b-1", "Board 1", null, null));
        var boards = await service.GetBoardsAsync();

        board.ExternalBoardId.Should().Be("b-1");
        boards.Should().ContainSingle();
    }

    [Fact]
    public async Task GetBoardsByTournament_ShouldIncludeBoardsAssignedViaMatches()
    {
        await using var dbContext = CreateDbContext();

        var tournamentId = Guid.NewGuid();
        var boardAssignedViaTournament = new Board
        {
            ExternalBoardId = "b-1",
            Name = "Rot",
            TournamentId = tournamentId
        };
        var boardAssignedViaMatch = new Board
        {
            ExternalBoardId = "b-2",
            Name = "Wonderland"
        };
        var unrelatedBoard = new Board
        {
            ExternalBoardId = "b-3",
            Name = "Andere"
        };

        dbContext.Boards.AddRange(boardAssignedViaTournament, boardAssignedViaMatch, unrelatedBoard);
        dbContext.Matches.Add(new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            Round = 1,
            MatchNumber = 1,
            BoardId = boardAssignedViaMatch.Id,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid(),
            Status = MatchStatus.Geplant
        });

        await dbContext.SaveChangesAsync();

        var service = new BoardManagementService(dbContext);
        var boards = await service.GetBoardsByTournamentAsync(tournamentId);

        boards.Select(x => x.Name).Should().BeEquivalentTo(["Rot", "Wonderland"]);
    }
}
