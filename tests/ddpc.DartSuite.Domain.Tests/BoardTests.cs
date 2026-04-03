using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using FluentAssertions;

namespace ddpc.DartSuite.Domain.Tests;

public sealed class BoardTests
{
    [Fact]
    public void ComputeOverallStatus_Offline_ReturnsError()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Offline;
        board.ConnectionState = ConnectionState.Online;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Error);
    }

    [Fact]
    public void ComputeOverallStatus_ErrorStatus_ReturnsError()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Error;
        board.ConnectionState = ConnectionState.Online;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Error);
    }

    [Fact]
    public void ComputeOverallStatus_ConnectionOffline_ReturnsError()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Running;
        board.ConnectionState = ConnectionState.Offline;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Error);
    }

    [Fact]
    public void ComputeOverallStatus_Delayed_ReturnsError()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Running;
        board.ConnectionState = ConnectionState.Online;
        board.SchedulingStatus = SchedulingStatus.Delayed;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Error);
    }

    [Fact]
    public void ComputeOverallStatus_Starting_ReturnsWarning()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Starting;
        board.ConnectionState = ConnectionState.Online;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Warning);
    }

    [Fact]
    public void ComputeOverallStatus_Ahead_ReturnsWarning()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Running;
        board.ConnectionState = ConnectionState.Online;
        board.SchedulingStatus = SchedulingStatus.Ahead;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Warning);
    }

    [Fact]
    public void ComputeOverallStatus_ActiveMatchWithoutListening_ReturnsWarning()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Running;
        board.ConnectionState = ConnectionState.Online;
        board.CurrentMatchId = Guid.NewGuid();
        board.ExtensionStatus = ExtensionConnectionStatus.Connected;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Warning);
    }

    [Fact]
    public void ComputeOverallStatus_RunningOnlineNoMatch_ReturnsOk()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Running;
        board.ConnectionState = ConnectionState.Online;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Ok);
    }

    [Fact]
    public void ComputeOverallStatus_RunningOnlineWithListening_ReturnsOk()
    {
        var board = CreateBoard();
        board.Status = BoardStatus.Running;
        board.ConnectionState = ConnectionState.Online;
        board.CurrentMatchId = Guid.NewGuid();
        board.ExtensionStatus = ExtensionConnectionStatus.Listening;

        board.ComputeOverallStatus().Should().Be(OverallBoardStatus.Ok);
    }

    private static Board CreateBoard() => new()
    {
        ExternalBoardId = "test-board",
        Name = "Test Board"
    };
}
