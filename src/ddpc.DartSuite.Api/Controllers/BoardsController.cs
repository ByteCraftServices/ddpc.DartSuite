using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.Api.Services;
using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.ApiClient.Contracts;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/boards")]
public sealed class BoardsController(
    IBoardManagementService boardService,
    IMatchManagementService matchService,
    IAutodartsClient autodartsClient,
    AutodartsSessionStore sessionStore,
    AutodartsMatchListenerService listenerService,
    BoardExtensionSyncRequestStore syncRequestStore,
    DartSuiteDbContext dbContext,
    IHubContext<BoardStatusHub> hubContext,
    IHubContext<TournamentHub> tournamentHubContext,
    ILogger<BoardsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await boardService.GetBoardsAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<BoardDto>> Create([FromBody] CreateBoardRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var board = await boardService.CreateBoardAsync(request, cancellationToken);
            await hubContext.Clients.All.SendAsync("BoardAdded", board, cancellationToken);
            return Ok(board);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BoardDto>> Update(Guid id, [FromBody] UpdateBoardRequest request, CancellationToken cancellationToken)
    {
        if (request.Id != id) return BadRequest("Id mismatch.");
        var board = await boardService.UpdateBoardAsync(request, cancellationToken);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await boardService.DeleteBoardAsync(id, cancellationToken);
            if (deleted) await hubContext.Clients.All.SendAsync("BoardRemoved", id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<BoardDto>> UpdateStatus(Guid id, [FromQuery] string status, [FromQuery] string? externalMatchId, CancellationToken cancellationToken)
    {
        var board = await boardService.UpdateBoardStatusAsync(id, status, externalMatchId, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardStatusChanged", board, cancellationToken);

        // Auto-start listener: if externalMatchId was provided, use it directly;
        // otherwise check if the current match already has an ExternalMatchId
        if (board.CurrentMatchId.HasValue)
        {
            if (!string.IsNullOrEmpty(externalMatchId))
            {
                listenerService.EnsureListener(board.CurrentMatchId.Value, externalMatchId, board.Id);
            }
            else
            {
                var match = await matchService.GetMatchAsync(board.CurrentMatchId.Value, cancellationToken);
                if (match is not null && !string.IsNullOrEmpty(match.ExternalMatchId) && match.FinishedUtc is null)
                {
                    listenerService.EnsureListener(match.Id, match.ExternalMatchId, board.Id);
                }
            }
        }

        return Ok(board);
    }

    [HttpPatch("{id:guid}/managed")]
    public async Task<ActionResult<BoardDto>> SetManagedMode(Guid id, [FromQuery] string mode, [FromQuery] Guid? tournamentId, CancellationToken cancellationToken)
    {
        var board = await boardService.SetManagedModeAsync(id, mode, tournamentId, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardManagedModeChanged", board, cancellationToken);
        return Ok(board);
    }

    [HttpPatch("{id:guid}/current-match")]
    public async Task<ActionResult<BoardDto>> SetCurrentMatch(Guid id, [FromQuery] Guid? matchId, [FromQuery] string? matchLabel, CancellationToken cancellationToken)
    {
        var board = await boardService.SetCurrentMatchAsync(id, matchId, matchLabel, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardCurrentMatchChanged", board, cancellationToken);
        return Ok(board);
    }

    [HttpPatch("{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id, CancellationToken cancellationToken)
    {
        var result = await boardService.HeartbeatAsync(id, cancellationToken);
        return result ? Ok() : NotFound();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoardDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var board = await boardService.GetBoardAsync(id, cancellationToken);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpPatch("{id:guid}/connection-state")]
    public async Task<ActionResult<BoardDto>> UpdateConnectionState(Guid id, [FromQuery] string state, CancellationToken cancellationToken)
    {
        var board = await boardService.UpdateConnectionStateAsync(id, state, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardStatusChanged", board, cancellationToken);
        return Ok(board);
    }

    [HttpPatch("{id:guid}/extension-status")]
    public async Task<ActionResult<BoardDto>> UpdateExtensionStatus(Guid id, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var board = await boardService.UpdateExtensionStatusAsync(id, status, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardStatusChanged", board, cancellationToken);
        return Ok(board);
    }

    [HttpPost("{id:guid}/extension-sync/request")]
    public async Task<IActionResult> RequestExtensionSync(Guid id, CancellationToken cancellationToken)
    {
        var board = await boardService.GetBoardAsync(id, cancellationToken);
        if (board is null) return NotFound();

        var request = syncRequestStore.Request(id);

        logger.LogInformation(
            "Extension sync request accepted board={BoardId} requestId={RequestId} tournamentId={TournamentId} currentMatchId={CurrentMatchId} externalBoardId={ExternalBoardId}",
            id,
            request.RequestId,
            board.TournamentId,
            board.CurrentMatchId,
            board.ExternalBoardId);

        return Accepted(new ExtensionSyncRequestAcceptedResponse(true, request.RequestId, request.RequestedAtUtc));
    }

    [HttpPost("{id:guid}/extension-sync/consume")]
    public ActionResult<ExtensionSyncConsumeResponse> ConsumeExtensionSync(Guid id)
    {
        var consume = syncRequestStore.Consume(id);

        logger.LogInformation(
            "Extension sync consume response board={BoardId} shouldSync={ShouldSync} requestId={RequestId} requestedAtUtc={RequestedAtUtc}",
            id,
            consume.ShouldSync,
            consume.RequestId,
            consume.RequestedAtUtc);

        return Ok(new ExtensionSyncConsumeResponse(consume.ShouldSync, consume.RequestId, consume.RequestedAtUtc));
    }

    [HttpPost("{id:guid}/extension-sync/report")]
    public async Task<IActionResult> ReportExtensionSync(Guid id, [FromBody] BoardExtensionSyncReportRequest request, CancellationToken cancellationToken)
    {
        var board = await dbContext.Boards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (board is null) return NotFound();

        logger.LogInformation(
            "Extension sync report board={BoardId}, requestId={RequestId}, tournament={TournamentId}, url={SourceUrl}, externalMatchId={ExternalMatchId}, player1={Player1}, player2={Player2}, matchStatus={MatchStatus}",
            id,
            request.RequestId,
            request.TournamentId,
            request.SourceUrl,
            request.ExternalMatchId,
            request.Player1,
            request.Player2,
            request.MatchStatus);

        var now = DateTimeOffset.UtcNow;
        board.LastExtensionPollUtc = now;
        board.UpdatedUtc = now;

        if (request.TournamentId.HasValue && board.TournamentId != request.TournamentId)
            board.TournamentId = request.TournamentId;

        var tournamentId = request.TournamentId ?? board.TournamentId;
        var resolvedPlayer1 = request.Player1;
        var resolvedPlayer2 = request.Player2;

        if ((string.IsNullOrWhiteSpace(resolvedPlayer1) || string.IsNullOrWhiteSpace(resolvedPlayer2))
            && !string.IsNullOrWhiteSpace(request.ExternalMatchId))
        {
            var externalPlayers = await TryResolvePlayersFromExternalMatchAsync(request.ExternalMatchId.Trim(), cancellationToken);
            if (externalPlayers is not null)
            {
                resolvedPlayer1 ??= externalPlayers.Value.Player1;
                resolvedPlayer2 ??= externalPlayers.Value.Player2;

                logger.LogInformation(
                    "Extension sync player fallback board={BoardId} requestId={RequestId} externalMatchId={ExternalMatchId} player1={Player1} player2={Player2}",
                    id,
                    request.RequestId,
                    request.ExternalMatchId,
                    resolvedPlayer1,
                    resolvedPlayer2);
            }
        }

        var derivedStatus = DeriveStatusFromExtension(request.SourceUrl, request.MatchStatus);

        Match? match = null;
        var matchedBy = "none";
        if (tournamentId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(request.ExternalMatchId))
            {
                var externalMatchId = request.ExternalMatchId.Trim();
                match = await dbContext.Matches
                    .Where(x => x.TournamentId == tournamentId.Value && x.FinishedUtc == null)
                    .FirstOrDefaultAsync(x => x.ExternalMatchId == externalMatchId, cancellationToken);
                if (match is not null)
                    matchedBy = "externalMatchId";
            }

            if (match is null)
            {
                match = await FindOpenMatchByPlayersAsync(
                    tournamentId.Value,
                    resolvedPlayer1,
                    resolvedPlayer2,
                    board.Id,
                    board.CurrentMatchId,
                    cancellationToken);
                if (match is not null)
                    matchedBy = "players";
            }

            if (match is null && board.CurrentMatchId.HasValue)
            {
                match = await dbContext.Matches.FirstOrDefaultAsync(
                    x => x.Id == board.CurrentMatchId.Value && x.FinishedUtc == null,
                    cancellationToken);
                if (match is not null)
                    matchedBy = "boardCurrentMatchId";
            }
        }

        if (match is not null)
        {
            match.BoardId = board.Id;
            board.CurrentMatchId = match.Id;

            var homeName = await ResolveParticipantNameAsync(match.HomeParticipantId, cancellationToken);
            var awayName = await ResolveParticipantNameAsync(match.AwayParticipantId, cancellationToken);
            board.CurrentMatchLabel = $"{homeName} vs {awayName}";

            if (!string.IsNullOrWhiteSpace(request.ExternalMatchId))
                match.ExternalMatchId = request.ExternalMatchId.Trim();

            if (match.FinishedUtc is null)
            {
                if (derivedStatus == MatchStatus.Aktiv)
                {
                    match.StartedUtc ??= now;
                    match.Status = MatchStatus.Aktiv;
                }
                else if (derivedStatus == MatchStatus.Warten && match.StartedUtc is null)
                {
                    match.Status = MatchStatus.Warten;
                }
                else if (derivedStatus == MatchStatus.Geplant && match.StartedUtc is null && match.Status != MatchStatus.WalkOver)
                {
                    match.Status = MatchStatus.Geplant;
                }
            }

            match.RecomputeStatus();

            logger.LogInformation(
                "Extension sync board={BoardId} matched match={MatchId} by={MatchedBy} status={Status} derivedStatus={DerivedStatus}",
                id,
                match.Id,
                matchedBy,
                match.Status,
                derivedStatus);
        }
        else
        {
            logger.LogWarning("Extension sync board={BoardId} did not match any open match. boardCurrentMatchId={CurrentMatchId}", id, board.CurrentMatchId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var boardDto = await boardService.GetBoardAsync(board.Id, cancellationToken);
        if (boardDto is not null)
        {
            await hubContext.Clients.All.SendAsync("BoardCurrentMatchChanged", boardDto, cancellationToken);
            await hubContext.Clients.All.SendAsync("BoardStatusChanged", boardDto, cancellationToken);
        }

        if (match is not null)
        {
            await NotifyTournamentAsync(match.TournamentId, "MatchUpdated", cancellationToken);

            if (!string.IsNullOrWhiteSpace(match.ExternalMatchId) && match.FinishedUtc is null)
                listenerService.EnsureListener(match.Id, match.ExternalMatchId!, board.Id);
        }

        var response = new ExtensionSyncReportResponse(
            Matched: match is not null,
            MatchId: match?.Id,
            MatchedBy: matchedBy,
            RequestId: request.RequestId,
            BoardId: id,
            BoardCurrentMatchId: board.CurrentMatchId,
            BoardCurrentMatchLabel: board.CurrentMatchLabel,
            DerivedStatus: derivedStatus.ToString(),
            TournamentId: tournamentId,
            ExternalMatchId: request.ExternalMatchId,
            Player1: request.Player1,
            Player1: resolvedPlayer1,
            Player2: resolvedPlayer2,
            MatchStatus: request.MatchStatus,
            SourceUrl: request.SourceUrl);

        logger.LogInformation(
            "Extension sync response board={BoardId} requestId={RequestId} matched={Matched} matchId={MatchId} matchedBy={MatchedBy} derivedStatus={DerivedStatus} boardCurrentMatchId={BoardCurrentMatchId}",
            response.BoardId,
            response.RequestId,
            response.Matched,
            response.MatchId,
            response.MatchedBy,
            response.DerivedStatus,
            response.BoardCurrentMatchId);

        var existingTelemetry = syncRequestStore.GetLatest(id);
        syncRequestStore.SetLatestReport(new BoardExtensionSyncRequestStore.SyncTelemetry(
            BoardId: response.BoardId,
            RequestId: response.RequestId ?? existingTelemetry?.RequestId,
            RequestedAtUtc: existingTelemetry?.RequestedAtUtc,
            ConsumedAtUtc: existingTelemetry?.ConsumedAtUtc,
            ShouldSync: existingTelemetry?.ShouldSync,
            ReportedAtUtc: DateTimeOffset.UtcNow,
            Matched: response.Matched,
            MatchId: response.MatchId,
            MatchedBy: response.MatchedBy,
            DerivedStatus: response.DerivedStatus,
            BoardCurrentMatchId: response.BoardCurrentMatchId,
            BoardCurrentMatchLabel: response.BoardCurrentMatchLabel,
            ExternalMatchId: response.ExternalMatchId,
            Player1: response.Player1,
            Player2: response.Player2,
            MatchStatus: response.MatchStatus,
            SourceUrl: response.SourceUrl,
            TournamentId: response.TournamentId));

        return Ok(response);
    }

    [HttpGet("{id:guid}/extension-sync/last")]
    public ActionResult<ExtensionSyncDebugResponse> GetLastExtensionSync(Guid id)
    {
        var telemetry = syncRequestStore.GetLatest(id);
        if (telemetry is null) return NotFound();

        return Ok(new ExtensionSyncDebugResponse(
            telemetry.BoardId,
            telemetry.RequestId,
            telemetry.RequestedAtUtc,
            telemetry.ConsumedAtUtc,
            telemetry.ShouldSync,
            telemetry.ReportedAtUtc,
            telemetry.Matched,
            telemetry.MatchId,
            telemetry.MatchedBy,
            telemetry.DerivedStatus,
            telemetry.BoardCurrentMatchId,
            telemetry.BoardCurrentMatchLabel,
            telemetry.ExternalMatchId,
            telemetry.Player1,
            telemetry.Player2,
            telemetry.MatchStatus,
            telemetry.SourceUrl,
            telemetry.TournamentId));
    }

    [HttpGet("tournament/{tournamentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetByTournament(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await boardService.GetBoardsByTournamentAsync(tournamentId, cancellationToken));
    }

    private async Task<Match?> FindOpenMatchByPlayersAsync(
        Guid tournamentId,
        string? player1,
        string? player2,
        Guid boardId,
        Guid? preferredMatchId,
        CancellationToken cancellationToken)
    {
        var normalizedP1 = NormalizePlayerName(player1);
        var normalizedP2 = NormalizePlayerName(player2);
        if (string.IsNullOrEmpty(normalizedP1) || string.IsNullOrEmpty(normalizedP2))
            return null;

        var participants = await dbContext.Participants
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var p1Id = ResolveParticipantId(participants, normalizedP1);
        var p2Id = ResolveParticipantId(participants, normalizedP2);
        if (!p1Id.HasValue || !p2Id.HasValue)
            return null;

        return await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.FinishedUtc == null && x.Status != MatchStatus.WalkOver)
            .Where(x =>
                (x.HomeParticipantId == p1Id.Value && x.AwayParticipantId == p2Id.Value)
                || (x.HomeParticipantId == p2Id.Value && x.AwayParticipantId == p1Id.Value))
            .OrderByDescending(x => preferredMatchId.HasValue && x.Id == preferredMatchId.Value)
            .ThenByDescending(x => x.BoardId.HasValue && x.BoardId.Value == boardId)
            .ThenByDescending(x => x.Status == MatchStatus.Aktiv || x.Status == MatchStatus.Warten)
            .ThenByDescending(x => x.StartedUtc.HasValue)
            .ThenBy(x => x.PlannedStartUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.MatchNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> ResolveParticipantNameAsync(Guid participantId, CancellationToken cancellationToken)
    {
        var participant = await dbContext.Participants.FirstOrDefaultAsync(x => x.Id == participantId, cancellationToken);
        if (participant is null) return "?";
        return !string.IsNullOrWhiteSpace(participant.DisplayName) ? participant.DisplayName : participant.AccountName;
    }

    private async Task<(string? Player1, string? Player2)?> TryResolvePlayersFromExternalMatchAsync(string externalMatchId, CancellationToken cancellationToken)
    {
        var accessToken = await GetActiveAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            var match = await autodartsClient.GetMatchAsync(accessToken, externalMatchId, cancellationToken);
            if (match is null)
                return null;

            var players = ExtractPlayerNames(match);
            return players.Length >= 2 ? (players[0], players[1]) : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            logger.LogWarning(ex, "Failed to resolve external match players for {ExternalMatchId}", externalMatchId);
            return null;
        }
    }

    private async Task<string?> GetActiveAccessTokenAsync(CancellationToken cancellationToken)
    {
        var session = sessionStore.GetActive();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
            return null;

        if (!sessionStore.IsTokenExpired(session.SessionId))
            return session.AccessToken;

        if (!string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            try
            {
                var newToken = await autodartsClient.RefreshAccessTokenAsync(session.RefreshToken!, cancellationToken);
                var expiresAt = DateTimeOffset.UtcNow.AddSeconds(newToken.ExpiresIn > 0 ? newToken.ExpiresIn : 3600);
                sessionStore.UpdateTokens(session.SessionId, newToken.AccessToken, newToken.RefreshToken, expiresAt);
                return newToken.AccessToken;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Token refresh failed while resolving board sync players: {Message}", ex.Message);
            }
        }

        return session.AccessToken;
    }

    private static string[] ExtractPlayerNames(AutodartsMatchDetail match)
    {
        if (match.RawJson.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return [];

        if (!match.RawJson.TryGetProperty("players", out var playersElement) || playersElement.ValueKind != JsonValueKind.Array)
            return [];

        return playersElement.EnumerateArray()
            .Select(ExtractPlayerName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToArray();
    }

    private static string? ExtractPlayerName(JsonElement player)
    {
        if (player.ValueKind == JsonValueKind.String)
            return player.GetString();

        if (player.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var candidate in new[] { "name", "displayName", "accountName", "username" })
        {
            if (player.TryGetProperty(candidate, out var property) && property.ValueKind == JsonValueKind.String)
                return property.GetString();
        }

        if (player.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in new[] { "name", "displayName", "accountName", "username" })
            {
                if (user.TryGetProperty(candidate, out var property) && property.ValueKind == JsonValueKind.String)
                    return property.GetString();
            }
        }

        return null;
    }

    private static Guid? ResolveParticipantId(IEnumerable<Participant> participants, string normalizedName)
    {
        foreach (var participant in participants)
        {
            if (NormalizePlayerName(participant.DisplayName) == normalizedName
                || NormalizePlayerName(participant.AccountName) == normalizedName)
                return participant.Id;
        }

        return null;
    }

    private static string NormalizePlayerName(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static MatchStatus DeriveStatusFromExtension(string? sourceUrl, string? extensionMatchStatus)
    {
        var url = sourceUrl?.Trim() ?? string.Empty;
        var status = extensionMatchStatus?.Trim() ?? string.Empty;

        if (url.Contains("/matches/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "playing", StringComparison.OrdinalIgnoreCase))
            return MatchStatus.Aktiv;

        if ((url.Contains("/lobbies/", StringComparison.OrdinalIgnoreCase)
                && !url.Contains("/lobbies/new", StringComparison.OrdinalIgnoreCase))
            || string.Equals(status, "waitForPlayer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "waitForMatch", StringComparison.OrdinalIgnoreCase))
            return MatchStatus.Warten;

        return MatchStatus.Geplant;
    }

    private Task NotifyTournamentAsync(Guid tournamentId, string method, CancellationToken cancellationToken)
        => tournamentHubContext.Clients.Group($"tournament-{tournamentId}").SendAsync(method, tournamentId.ToString(), cancellationToken);

    public sealed record ExtensionSyncConsumeResponse(bool ShouldSync, Guid? RequestId, DateTimeOffset? RequestedAtUtc);

    public sealed record ExtensionSyncRequestAcceptedResponse(bool Requested, Guid RequestId, DateTimeOffset RequestedAtUtc);

    public sealed record BoardExtensionSyncReportRequest(
        Guid? RequestId,
        Guid? TournamentId,
        string? SourceUrl,
        string? ExternalMatchId,
        string? Player1,
        string? Player2,
        string? MatchStatus);

    public sealed record ExtensionSyncReportResponse(
        bool Matched,
        Guid? MatchId,
        string MatchedBy,
        Guid? RequestId,
        Guid BoardId,
        Guid? BoardCurrentMatchId,
        string? BoardCurrentMatchLabel,
        string DerivedStatus,
        Guid? TournamentId,
        string? ExternalMatchId,
        string? Player1,
        string? Player2,
        string? MatchStatus,
        string? SourceUrl);

    public sealed record ExtensionSyncDebugResponse(
        Guid BoardId,
        Guid? RequestId,
        DateTimeOffset? RequestedAtUtc,
        DateTimeOffset? ConsumedAtUtc,
        bool? ShouldSync,
        DateTimeOffset? ReportedAtUtc,
        bool? Matched,
        Guid? MatchId,
        string? MatchedBy,
        string? DerivedStatus,
        Guid? BoardCurrentMatchId,
        string? BoardCurrentMatchLabel,
        string? ExternalMatchId,
        string? Player1,
        string? Player2,
        string? MatchStatus,
        string? SourceUrl,
        Guid? TournamentId);
}