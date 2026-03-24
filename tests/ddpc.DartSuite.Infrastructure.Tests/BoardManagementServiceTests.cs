using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class BoardManagementServiceTests
{
    [Fact]
    public async Task CreateBoard_ShouldPersistBoard()
    {
        var options = new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new DartSuiteDbContext(options);
        var service = new BoardManagementService(dbContext);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("b-1", "Board 1", null, null));
        var boards = await service.GetBoardsAsync();

        board.ExternalBoardId.Should().Be("b-1");
        boards.Should().ContainSingle();
    }
}