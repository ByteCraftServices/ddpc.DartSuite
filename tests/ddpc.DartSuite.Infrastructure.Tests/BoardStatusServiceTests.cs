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

    // ─── UpdateBoardStatusAsync: all 5 BoardStatus values ───

    [Theory]
    [InlineData("Offline")]
    [InlineData("Starting")]
    [InlineData("Running")]
    [InlineData("Online")]
    [InlineData("Error")]
    public async Task UpdateBoardStatus_AllValues_ShouldPersist(string status)
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateBoardStatusAsync(board.Id, status);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(status);
    }

    [Fact]
    public async Task UpdateBoardStatus_InvalidValue_DefaultsToOffline()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateBoardStatusAsync(board.Id, "completely-unknown-value");

        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Offline");
    }

    [Fact]
    public async Task UpdateBoardStatus_CaseInsensitive_ShouldParsed()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateBoardStatusAsync(board.Id, "running");

        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Running");
    }

    [Fact]
    public async Task UpdateBoardStatus_Running_ReturnsExpectedOverallStatus()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        // Set ConnectionState to Online first so OverallStatus can be Ok
        await service.UpdateConnectionStateAsync(board.Id, "Online");
        var updated = await service.UpdateBoardStatusAsync(board.Id, "Running");

        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Running");
        updated.OverallStatus.Should().Be("Ok");
    }

    [Fact]
    public async Task UpdateBoardStatus_Offline_ReturnsErrorOverallStatus()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        await service.UpdateConnectionStateAsync(board.Id, "Online");
        var updated = await service.UpdateBoardStatusAsync(board.Id, "Offline");

        updated.Should().NotBeNull();
        updated!.OverallStatus.Should().Be("Error");
    }

    // ─── UpdateExtensionStatusAsync: all 3 ExtensionConnectionStatus values ───

    [Theory]
    [InlineData("Offline")]
    [InlineData("Connected")]
    [InlineData("Listening")]
    public async Task UpdateExtensionStatus_AllValues_ShouldPersist(string status)
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateExtensionStatusAsync(board.Id, status);

        updated.Should().NotBeNull();
        updated!.ExtensionStatus.Should().Be(status);
    }

    [Fact]
    public async Task UpdateExtensionStatus_ListeningWithActiveMatch_ReturnsOkOverallStatus()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        await service.UpdateBoardStatusAsync(board.Id, "Running");
        await service.UpdateConnectionStateAsync(board.Id, "Online");

        // Simulate an active match
        var dbBoard = await db.Boards.FindAsync(board.Id);
        dbBoard!.CurrentMatchId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var updated = await service.UpdateExtensionStatusAsync(board.Id, "Listening");

        updated.Should().NotBeNull();
        updated!.ExtensionStatus.Should().Be("Listening");
        updated.OverallStatus.Should().Be("Ok");
    }

    [Fact]
    public async Task UpdateExtensionStatus_ConnectedWithActiveMatch_ReturnsWarningOverallStatus()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        await service.UpdateBoardStatusAsync(board.Id, "Running");
        await service.UpdateConnectionStateAsync(board.Id, "Online");

        var dbBoard = await db.Boards.FindAsync(board.Id);
        dbBoard!.CurrentMatchId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var updated = await service.UpdateExtensionStatusAsync(board.Id, "Connected");

        updated.Should().NotBeNull();
        updated!.OverallStatus.Should().Be("Warning");
    }

    // ─── UpdateConnectionStateAsync: Online/Offline ───

    [Theory]
    [InlineData("Online")]
    [InlineData("Offline")]
    public async Task UpdateConnectionState_AllValues_ShouldPersist(string state)
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        var updated = await service.UpdateConnectionStateAsync(board.Id, state);

        updated.Should().NotBeNull();
        updated!.ConnectionState.Should().Be(state);
    }

    [Fact]
    public async Task UpdateConnectionState_Offline_ReturnsErrorEvenWhenRunning()
    {
        await using var db = CreateDbContext();
        var service = new BoardManagementService(db);

        var board = await service.CreateBoardAsync(new CreateBoardRequest("ext-1", "Board 1", null, null));
        await service.UpdateBoardStatusAsync(board.Id, "Running");
        var updated = await service.UpdateConnectionStateAsync(board.Id, "Offline");

        updated.Should().NotBeNull();
        updated!.OverallStatus.Should().Be("Error");
    }
}
