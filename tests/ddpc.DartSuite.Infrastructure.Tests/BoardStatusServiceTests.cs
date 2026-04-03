using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class BoardStatusServiceTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateBoard_ShouldReturnOverallStatus()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));

        board.OverallStatus.Should().NotBeNullOrEmpty();
        board.ConnectionState.Should().Be("Offline");
        board.ExtensionStatus.Should().Be("Offline");
        board.SchedulingStatus.Should().Be("None");
    }

    [Fact]
    public async Task UpdateConnectionState_ShouldPersist()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateConnectionStateAsync(board.Id, "Online");

        updated.Should().NotBeNull();
        updated!.ConnectionState.Should().Be("Online");
    }

    [Fact]
    public async Task UpdateExtensionStatus_ShouldPersist()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateExtensionStatusAsync(board.Id, "Connected");

        updated.Should().NotBeNull();
        updated!.ExtensionStatus.Should().Be("Connected");
    }

    [Fact]
    public async Task GetBoard_ShouldReturnBoardById()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var created = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var board = await service.GetBoardAsync(created.Id);

        board.Should().NotBeNull();
        board!.Id.Should().Be(created.Id);
        board.Name.Should().Be("Board 1");
    }

    [Fact]
    public async Task GetBoard_NonExistent_ShouldReturnNull()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.GetBoardAsync(Guid.NewGuid());

        board.Should().BeNull();
    }
}
