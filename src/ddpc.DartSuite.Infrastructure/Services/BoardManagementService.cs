using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class BoardManagementService(DartSuiteDbContext dbContext) : IBoardManagementService
{
    private static bool IsExtensionConnected(Board b) =>
        b.LastExtensionPollUtc.HasValue &&
        (DateTimeOffset.UtcNow - b.LastExtensionPollUtc.Value).TotalSeconds < 30;

    private static BoardDto ToDto(Board b) => new(b.Id, b.ExternalBoardId, b.Name, b.Status.ToString(),
        b.ConnectionState.ToString(), b.ExtensionStatus.ToString(), b.SchedulingStatus.ToString(),
        b.ComputeOverallStatus().ToString(),
        b.LocalIpAddress, b.BoardManagerUrl, b.CurrentMatchId, b.CurrentMatchLabel,
        b.ManagedMode.ToString(), b.TournamentId, b.UpdatedUtc, IsExtensionConnected(b));

    public async Task<IReadOnlyList<BoardDto>> GetBoardsAsync(CancellationToken cancellationToken = default)
    {
        var boards = await dbContext.Boards.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return boards.Select(ToDto).ToList();
    }

    public async Task<BoardDto> CreateBoardAsync(CreateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Boards.AnyAsync(x => x.ExternalBoardId == request.ExternalBoardId, cancellationToken);
        if (exists) throw new InvalidOperationException("Board already exists.");

        var board = new Board
        {
            ExternalBoardId = request.ExternalBoardId,
            Name = request.Name,
            LocalIpAddress = request.LocalIpAddress,
            BoardManagerUrl = request.BoardManagerUrl,
            Status = BoardStatus.Starting,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<BoardDto?> UpdateBoardAsync(UpdateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (board is null) return null;

        board.Name = request.Name;
        board.LocalIpAddress = request.LocalIpAddress;
        board.BoardManagerUrl = request.BoardManagerUrl;
        board.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<bool> DeleteBoardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return false;

        // Block deletion if board has active (started but not finished) matches
        var hasActiveMatches = await dbContext.Matches.AnyAsync(
            m => m.BoardId == id && m.StartedUtc != null && m.FinishedUtc == null, cancellationToken);
        if (hasActiveMatches)
            throw new InvalidOperationException("Das Board hat laufende Matches und kann nicht entfernt werden.");

        // Reassign future (not started, not finished) matches to remaining boards
        var futureMatches = await dbContext.Matches
            .Where(m => m.BoardId == id && m.StartedUtc == null && m.FinishedUtc == null && !m.IsBoardLocked)
            .ToListAsync(cancellationToken);

        if (futureMatches.Count > 0)
        {
            var otherBoards = await dbContext.Boards
                .Where(b => b.Id != id)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);

            if (otherBoards.Count > 0)
            {
                var idx = 0;
                foreach (var match in futureMatches)
                {
                    match.BoardId = otherBoards[idx % otherBoards.Count];
                    idx++;
                }
            }
            else
            {
                foreach (var match in futureMatches)
                    match.BoardId = null;
            }
        }

        dbContext.Boards.Remove(board);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BoardDto?> UpdateBoardStatusAsync(Guid id, string status, string? externalMatchId = null, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return null;

        board.Status = Enum.TryParse<BoardStatus>(status, true, out var s) ? s : BoardStatus.Offline;
        board.UpdatedUtc = DateTimeOffset.UtcNow;
        board.LastExtensionPollUtc = DateTimeOffset.UtcNow;

        // Store the Autodarts match ID on the DartSuite match for recovery & result fetching
        if (!string.IsNullOrEmpty(externalMatchId) && board.CurrentMatchId.HasValue)
        {
            var match = await dbContext.Matches.FirstOrDefaultAsync(
                x => x.Id == board.CurrentMatchId.Value, cancellationToken);
            if (match is not null && match.ExternalMatchId != externalMatchId)
            {
                match.ExternalMatchId = externalMatchId;
                if (match.StartedUtc is null)
                    match.StartedUtc = DateTimeOffset.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<BoardDto?> SetManagedModeAsync(Guid id, string mode, Guid? tournamentId, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return null;

        var parsed = Enum.TryParse<BoardManagedMode>(mode, true, out var m) ? m : BoardManagedMode.Manual;
        board.ManagedMode = parsed;
        board.TournamentId = parsed == BoardManagedMode.Auto ? tournamentId : null;
        board.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<BoardDto?> SetCurrentMatchAsync(Guid id, Guid? matchId, string? matchLabel, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return null;

        board.CurrentMatchId = matchId;
        board.CurrentMatchLabel = matchLabel;
        board.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<bool> HeartbeatAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return false;

        board.LastExtensionPollUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BoardDto?> GetBoardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return board is null ? null : ToDto(board);
    }

    public async Task<BoardDto?> UpdateConnectionStateAsync(Guid id, string connectionState, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return null;

        board.ConnectionState = Enum.TryParse<ConnectionState>(connectionState, true, out var cs) ? cs : ConnectionState.Offline;
        board.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<BoardDto?> UpdateExtensionStatusAsync(Guid id, string extensionStatus, CancellationToken cancellationToken = default)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return null;

        board.ExtensionStatus = Enum.TryParse<ExtensionConnectionStatus>(extensionStatus, true, out var es) ? es : ExtensionConnectionStatus.Offline;
        board.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<IReadOnlyList<BoardDto>> GetBoardsByTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var boards = await dbContext.Boards.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return boards.Select(ToDto).ToList();
    }
}