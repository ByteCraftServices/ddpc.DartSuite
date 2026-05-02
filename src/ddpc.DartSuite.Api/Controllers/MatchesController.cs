using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.ApiClient.Contracts;
using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.Api.Services;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(
    IMatchManagementService matchService,
    IAutodartsClient autodartsClient,
    AutodartsSessionStore sessionStore,
    AutodartsMatchListenerService listenerService,
    TournamentAuthorizationService tournamentAuthorization,
    DartSuiteDbContext dbContext,
    IHubContext<TournamentHub> tournamentHub,
    ILogger<MatchesController> logger) : ControllerBase
{
    private static long _matchMakerRealtimeSequence;

    [HttpGet("{tournamentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> Get(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GetMatchesAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/generate")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> Generate(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var result = await matchService.GenerateKnockoutPlanAsync(tournamentId, cancellationToken);
            await NotifyTournamentAsync(tournamentId, "MatchUpdated");
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{tournamentId:guid}/generate-groups")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GenerateGroups(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var result = await matchService.GenerateGroupPhaseAsync(tournamentId, cancellationToken);
            await NotifyTournamentAsync(tournamentId, "MatchUpdated");
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{tournamentId:guid}/group-standings")]
    public async Task<ActionResult<IReadOnlyList<GroupStandingDto>>> GroupStandings(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GetGroupStandingsAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/generate-schedule")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GenerateSchedule(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var result = await matchService.GenerateScheduleAsync(tournamentId, cancellationToken);
            await NotifyTournamentAsync(tournamentId, "ScheduleUpdated");
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{matchId:guid}/swap")]
    public async Task<IActionResult> SwapParticipants(Guid matchId, [FromQuery] Guid participantId, [FromQuery] Guid targetParticipantId, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        try
        {
            await matchService.SwapParticipantsAsync(matchId, participantId, targetParticipantId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{matchId:guid}/board")]
    public async Task<ActionResult<MatchDto>> AssignBoard(Guid matchId, [FromQuery] Guid boardId, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        try
        {
            var match = await matchService.AssignBoardAsync(matchId, boardId, cancellationToken);
            return match is null ? NotFound() : Ok(match);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{matchId:guid}/schedule")]
    public async Task<ActionResult<MatchDto>> UpdateSchedule(
        Guid matchId,
        [FromQuery] DateTimeOffset? startTime,
        [FromQuery] bool lockTime,
        [FromQuery] Guid? boardId,
        [FromQuery] bool lockBoard,
        CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        try
        {
            var match = await matchService.UpdateMatchScheduleAsync(matchId, startTime, lockTime, boardId, lockBoard, cancellationToken);
            return match is null ? NotFound() : Ok(match);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{matchId:guid}/lock-time")]
    public async Task<ActionResult<MatchDto>> ToggleTimeLock(Guid matchId, [FromQuery] bool locked, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var match = await matchService.ToggleMatchTimeLockAsync(matchId, locked, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPatch("{matchId:guid}/lock-board")]
    public async Task<ActionResult<MatchDto>> ToggleBoardLock(Guid matchId, [FromQuery] bool locked, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var match = await matchService.ToggleMatchBoardLockAsync(matchId, locked, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPost("result")]
    public async Task<ActionResult<MatchDto>> ReportResult([FromBody] ReportMatchResultRequest request, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(request.MatchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var match = await matchService.ReportResultAsync(request, cancellationToken);
        if (match is not null)
            await NotifyTournamentAsync(match.TournamentId, "MatchUpdated");
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPost("leg-result")]
    public ActionResult ReportLegResult([FromBody] LegResultRequest request)
    {
        return Ok(new { received = true, tournamentId = request.TournamentId, matchId = request.MatchId, leg = request.LegNumber });
    }

    [HttpPost("{matchId:guid}/sync-external")]
    public async Task<ActionResult<MatchDto>> SyncExternal(Guid matchId, CancellationToken cancellationToken)
    {
        // Look up the DartSuite match to get the ExternalMatchId
        var existingMatch = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (existingMatch is null) return NotFound();

        var denied = await RequireManagerAccessAsync(existingMatch.TournamentId, cancellationToken);
        if (denied is not null) return denied;

        if (string.IsNullOrEmpty(existingMatch.ExternalMatchId))
            return BadRequest(new { message = "Match has no ExternalMatchId." });

        // Get Autodarts access token (with refresh if needed)
        var accessToken = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
            return Unauthorized(new { message = "Not connected to Autodarts." });

        // Fetch match data from Autodarts API
        AutodartsMatchDetail? adMatch;
        try
        {
            adMatch = await autodartsClient.GetMatchAsync(accessToken, existingMatch.ExternalMatchId, allowLobbyFallback: false, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Autodarts API error during sync for match {MatchId}", matchId);
            return StatusCode((int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                new { message = $"Autodarts API Fehler: {ex.StatusCode} — {ex.Message}" });
        }

        if (adMatch is null)
            return NotFound(new { message = $"Autodarts match {existingMatch.ExternalMatchId} not found." });

        var participantIds = new[] { existingMatch.HomeParticipantId, existingMatch.AwayParticipantId };
        var participants = await dbContext.Participants
            .AsNoTracking()
            .Where(x => participantIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var homeParticipant = participants.FirstOrDefault(x => x.Id == existingMatch.HomeParticipantId);
        var awayParticipant = participants.FirstOrDefault(x => x.Id == existingMatch.AwayParticipantId);

        var mappedScores = AutodartsMatchScoreMapper.MapScores(
            adMatch,
            homeParticipant?.DisplayName ?? homeParticipant?.AccountName,
            awayParticipant?.DisplayName ?? awayParticipant?.AccountName);

        logger.LogInformation(
            "[SyncExternal] Match {MatchId} (ext: {ExternalMatchId}): {Home}-{Away} (Sets: {HomeSets}-{AwaySets}), finished={Finished}, mapping={MappingSource}, apiPlayers={ExternalPlayer1}|{ExternalPlayer2}, homeSlot={HomeSlot}, awaySlot={AwaySlot}",
            matchId,
            existingMatch.ExternalMatchId,
            mappedScores.HomeLegs,
            mappedScores.AwayLegs,
            mappedScores.HomeSets,
            mappedScores.AwaySets,
            adMatch.Finished,
            mappedScores.MappingSource,
            mappedScores.ExternalPlayer1,
            mappedScores.ExternalPlayer2,
            mappedScores.HomeSlot,
            mappedScores.AwaySlot);

        // Write full JSON to file for inspection (console truncates large JSON)
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var fileName = $"sync-{matchId:N}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(logDir, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, adMatch.RawJson.ToString(), cancellationToken);
            logger.LogInformation("[SyncExternal] API response written to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SyncExternal] Failed to write API response to file");
        }

        var result = await matchService.SyncMatchFromExternalAsync(
            matchId,
            mappedScores.HomeLegs,
            mappedScores.AwayLegs,
            mappedScores.HomeSets,
            mappedScores.AwaySets,
            adMatch.Finished,
            cancellationToken);
        if (result is not null)
            await NotifyTournamentAsync(result.TournamentId, "MatchUpdated");
        return result is null ? NotFound() : Ok(result);
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
                logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            }
        }

        return session.AccessToken;
    }

    [HttpGet("prediction")]
    public ActionResult<MatchPredictionDto> Prediction(
        [FromQuery] int targetLegs,
        [FromQuery] int homeLegs,
        [FromQuery] int awayLegs,
        [FromQuery] int homeScore,
        [FromQuery] int awayScore,
        [FromQuery] int elapsedSeconds)
    {
        var prediction = matchService.GetPrediction(targetLegs, homeLegs, awayLegs, homeScore, awayScore, TimeSpan.FromSeconds(elapsedSeconds));
        return Ok(prediction);
    }

    [HttpGet("listeners")]
    public ActionResult<IReadOnlyList<MatchListenerInfoDto>> GetListeners()
    {
        var listeners = listenerService.GetActiveListeners()
            .Values
            .Select(l => new MatchListenerInfoDto(
                l.MatchId,
                l.ExternalMatchId,
                l.BoardId,
                l.IsRunning,
                l.LastUpdateUtc,
                l.LastError,
                l.IsWebSocketActive,
                l.TransportMode,
                l.IsFallbackActive,
                l.LastRealtimeEventUtc))
            .ToList();
        return Ok(listeners);
    }

    [HttpPost("{matchId:guid}/listener")]
    public async Task<IActionResult> EnsureListener(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (match is null) return NotFound();

        var denied = await RequireManagerAccessAsync(match.TournamentId, cancellationToken);
        if (denied is not null) return denied;

        if (string.IsNullOrEmpty(match.ExternalMatchId))
            return BadRequest(new { message = "Match has no ExternalMatchId." });

        if (match.StartedUtc is null || match.FinishedUtc is not null)
            return BadRequest(new { message = "Monitoring darf nur fuer aktive Matches gestartet werden." });

        await listenerService.EnsureListenerAsync(matchId, match.ExternalMatchId, match.BoardId, cancellationToken: cancellationToken);
        return Ok(new { message = "Listener ensured." });
    }

    [HttpPost("{tournamentId:guid}/monitoring/reconcile")]
    public async Task<IActionResult> ReconcileMonitoring(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        await listenerService.ReconcileTournamentMonitoringAsync(tournamentId, cancellationToken);
        return Ok(new { message = "Monitoring reconciled." });
    }

    [HttpDelete("{matchId:guid}/listener")]
    public async Task<IActionResult> StopListener(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (match is null) return NotFound();

        var denied = await RequireManagerAccessAsync(match.TournamentId, cancellationToken);
        if (denied is not null) return denied;

        listenerService.StopListener(matchId);
        return NoContent();
    }

    [HttpPost("{matchId:guid}/reset")]
    public async Task<ActionResult<MatchDto>> ResetMatch(Guid matchId, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        // Stop any active listener before resetting
        listenerService.StopListener(matchId);

        var result = await matchService.ResetMatchAsync(matchId, cancellationToken);
        if (result is not null)
        {
            await NotifyTournamentAsync(result.TournamentId, "MatchUpdated");
            await NotifyTournamentAsync(result.TournamentId, "BoardsUpdated");
        }
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{matchId:guid}")]
    public async Task<ActionResult<MatchDto>> UpdateMatch(Guid matchId, [FromBody] UpdateMatchRequest request, CancellationToken cancellationToken)
    {
        if (matchId != request.MatchId) return BadRequest();

        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var result = await matchService.UpdateMatchAsync(request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("batch-reset")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> BatchReset([FromBody] IReadOnlyList<Guid> matchIds, CancellationToken cancellationToken)
    {
        foreach (var id in matchIds)
        {
            var access = await RequireManagerForMatchAsync(id, cancellationToken);
            if (access.Denied is not null) return access.Denied;
        }

        foreach (var id in matchIds)
            listenerService.StopListener(id);

        var result = await matchService.BatchResetMatchesAsync(matchIds, cancellationToken);
        foreach (var tournamentId in result.Select(r => r.TournamentId).Distinct())
        {
            await NotifyTournamentAsync(tournamentId, "MatchUpdated");
            await NotifyTournamentAsync(tournamentId, "BoardsUpdated");
        }
        return Ok(result);
    }

    [HttpPost("{tournamentId:guid}/cleanup")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> CleanupStale(Guid tournamentId, [FromQuery] int staleMinutes = 120, CancellationToken cancellationToken = default)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        var result = await matchService.CleanupStaleMatchesAsync(tournamentId, staleMinutes, cancellationToken);
        await listenerService.ReconcileTournamentMonitoringAsync(tournamentId, cancellationToken);
        await NotifyTournamentAsync(tournamentId, "MatchUpdated");
        await NotifyTournamentAsync(tournamentId, "BoardsUpdated");
        return Ok(result);
    }

    [HttpPost("{tournamentId:guid}/check-external")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> CheckExternalMatches(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        var accessToken = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
            return Unauthorized(new { message = "Not connected to Autodarts." });

        var matches = await matchService.GetMatchesAsync(tournamentId, cancellationToken);
        var withExternal = matches.Where(m => !string.IsNullOrEmpty(m.ExternalMatchId) && m.Status != "Beendet" && m.Status != "WalkOver").ToList();
        var invalidIds = new List<Guid>();

        foreach (var m in withExternal)
        {
            try
            {
                await autodartsClient.GetMatchAsync(accessToken, m.ExternalMatchId!, allowLobbyFallback: false, cancellationToken: cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                invalidIds.Add(m.Id);
            }
            catch
            {
                // Other errors (timeout, 500 etc.) — skip, don't mark as inactive
            }
        }

        var updated = new List<MatchDto>();
        foreach (var id in invalidIds)
        {
            var existing = withExternal.First(m => m.Id == id);
            var result = await matchService.UpdateMatchAsync(
                new UpdateMatchRequest(id, existing.BoardId, existing.HomeLegs, existing.AwayLegs,
                    existing.HomeSets, existing.AwaySets, "Inaktiv",
                    existing.IsStartTimeLocked, existing.IsBoardLocked, existing.WinnerParticipantId),
                cancellationToken);
            if (result is not null) updated.Add(result);
        }

        return Ok(updated);
    }

    // ─── Statistics (#18) ───

    [HttpGet("{matchId:guid}/statistics")]
    public async Task<ActionResult<IReadOnlyList<MatchPlayerStatisticDto>>> GetMatchStatistics(Guid matchId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GetMatchStatisticsAsync(matchId, cancellationToken));
    }

    [HttpPost("{matchId:guid}/statistics")]
    public async Task<ActionResult<MatchPlayerStatisticDto>> SaveStatistic(Guid matchId, [FromBody] MatchPlayerStatisticDto statistic, CancellationToken cancellationToken)
    {
        if (statistic.MatchId != matchId) return BadRequest("Match id mismatch.");

        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var result = await matchService.SaveMatchPlayerStatisticAsync(statistic, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{matchId:guid}/statistics/sync")]
    public async Task<ActionResult<IReadOnlyList<MatchPlayerStatisticDto>>> SyncStatistics(Guid matchId, CancellationToken cancellationToken)
    {
        var existingMatch = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (existingMatch is null) return NotFound();

        var denied = await RequireManagerAccessAsync(existingMatch.TournamentId, cancellationToken);
        if (denied is not null) return denied;

        if (string.IsNullOrWhiteSpace(existingMatch.ExternalMatchId))
            return BadRequest(new { message = "Match has no ExternalMatchId." });

        var accessToken = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
            return Unauthorized(new { message = "Not connected to Autodarts." });

        AutodartsMatchDetail? adMatch;
        try
        {
            adMatch = await autodartsClient.GetMatchAsync(accessToken, existingMatch.ExternalMatchId, allowLobbyFallback: false, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Autodarts API error during statistics sync for match {MatchId}", matchId);
            return StatusCode((int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                new { message = $"Autodarts API Fehler: {ex.StatusCode} - {ex.Message}" });
        }

        if (adMatch is null)
            return NotFound(new { message = $"Autodarts match {existingMatch.ExternalMatchId} not found." });

        var syncResult = await AutodartsMatchStatisticsSyncService.UpsertFromRawAsync(
            dbContext,
            matchId,
            existingMatch.HomeParticipantId,
            existingMatch.AwayParticipantId,
            adMatch.RawJson,
            AutodartsMatchStatisticsSyncService.ResolveSenderUtc(adMatch.RawJson, DateTimeOffset.UtcNow),
            existingMatch.HomeParticipantName,
            existingMatch.AwayParticipantName,
            cancellationToken);

        if (syncResult.Changed)
        {
            await tournamentHub.Clients.Group($"tournament-{existingMatch.TournamentId}").SendAsync(
                "MatchStatisticsUpdated",
                new
                {
                    tournamentId = existingMatch.TournamentId,
                    matchId,
                    sourceTimestamp = syncResult.SenderUtc,
                    timestamp = DateTimeOffset.UtcNow
                },
                cancellationToken);
        }

        return Ok(await matchService.GetMatchStatisticsAsync(matchId, cancellationToken));
    }

    // ─── Followers (#14) ───

    [HttpGet("{matchId:guid}/followers")]
    public async Task<ActionResult<IReadOnlyList<MatchFollowerDto>>> GetFollowers(Guid matchId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GetMatchFollowersAsync(matchId, cancellationToken));
    }

    [HttpPost("{matchId:guid}/follow")]
    public async Task<ActionResult<MatchFollowerDto>> FollowMatch(Guid matchId, [FromQuery] string userAccountName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userAccountName)) return BadRequest("userAccountName is required.");

        var access = await RequireSelfOrManagerForMatchAsync(matchId, userAccountName, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        return Ok(await matchService.FollowMatchAsync(matchId, userAccountName, cancellationToken));
    }

    [HttpDelete("{matchId:guid}/follow")]
    public async Task<IActionResult> UnfollowMatch(Guid matchId, [FromQuery] string userAccountName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userAccountName)) return BadRequest("userAccountName is required.");

        var access = await RequireSelfOrManagerForMatchAsync(matchId, userAccountName, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var result = await matchService.UnfollowMatchAsync(matchId, userAccountName, cancellationToken);
        return result ? NoContent() : NotFound();
    }

    // ─── Scheduling (#12) ───

    [HttpPost("{tournamentId:guid}/recalculate-schedule")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> RecalculateSchedule(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        var result = await matchService.RecalculateScheduleAsync(tournamentId, cancellationToken);
        await NotifyTournamentAsync(tournamentId, "ScheduleUpdated");
        return Ok(result);
    }

    private Task NotifyTournamentAsync(Guid tournamentId, string method)
    {
        return tournamentHub.Clients.Group($"tournament-{tournamentId}").SendAsync(method, tournamentId.ToString());
    }

    // ─── MatchMaker (Virtual Boards) ───

    /// <summary>
    /// Starts a match on a virtual board (replaces Chrome Extension "StartMatch").
    /// Marks the match as started and notifies all subscribers via SignalR.
    /// </summary>
    [HttpPost("{matchId:guid}/matchmaker/start")]
    public async Task<ActionResult<MatchDto>> MatchMakerStart(Guid matchId, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var match = access.Match!;

        // Validate the board is virtual
        if (match.BoardId.HasValue)
        {
            var board = await dbContext.Boards.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == match.BoardId.Value, cancellationToken);
            if (board is not null && !board.IsVirtual)
                return BadRequest(new { message = "MatchMaker ist nur für virtuelle Boards verfügbar." });
        }

        if (match.FinishedUtc.HasValue)
            return Conflict(new { message = "Match ist bereits beendet." });

        var result = await matchService.SyncMatchFromExternalAsync(
            matchId,
            match.HomeLegs, match.AwayLegs,
            match.HomeSets, match.AwaySets,
            false,
            cancellationToken);

        // Mark as started if not already
        if (result is not null && result.StartedUtc is null)
        {
            var entity = await dbContext.Matches.FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);
            if (entity is not null)
            {
                entity.StartedUtc = DateTimeOffset.UtcNow;
                entity.Status = Domain.Enums.MatchStatus.Aktiv;
                await dbContext.SaveChangesAsync(cancellationToken);
                result = await matchService.GetMatchAsync(matchId, cancellationToken);
            }
        }

        if (result is not null)
        {
            await NotifyTournamentAsync(result.TournamentId, "MatchUpdated");

            var roundCandidates = await dbContext.TournamentRounds
                .AsNoTracking()
                .Where(r => r.TournamentId == result.TournamentId && r.RoundNumber == result.Round)
                .ToListAsync(cancellationToken);
            var roundConfig = roundCandidates.FirstOrDefault(r => string.Equals(r.Phase.ToString(), result.Phase, StringComparison.OrdinalIgnoreCase))
                ?? roundCandidates.FirstOrDefault();
            var startScore = roundConfig?.BaseScore ?? 501;

            var startPayload = JsonSerializer.SerializeToElement(new
            {
                id = matchId.ToString(),
                eventType = "match-started",
                timestamp = DateTimeOffset.UtcNow,
                players = new[]
                {
                    new { score = startScore, points = startScore },
                    new { score = startScore, points = startScore }
                },
                turns = new[]
                {
                    new { player = 0, dart = 0, throws = Array.Empty<object>() }
                }
            });

            var startScores = new AutodartsMappedScoreResult(
                result.HomeLegs,
                result.AwayLegs,
                result.HomeSets,
                result.AwaySets,
                startScore,
                startScore,
                null,
                null,
                "matchmaker-start",
                0,
                1);

            await BroadcastMatchMakerRealtimeAsync(result, startPayload, startScores, finished: false, statisticsChanged: false, cancellationToken);
        }

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Receives a MatchMaker throw/state update (payload mirroring Autodarts API format).
    /// Updates match scores, statistics, and propagates changes via SignalR.
    /// </summary>
    [HttpPost("{matchId:guid}/matchmaker/throw")]
    public async Task<IActionResult> MatchMakerThrow(Guid matchId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var access = await RequireManagerForMatchAsync(matchId, cancellationToken);
        if (access.Denied is not null) return access.Denied;

        var match = access.Match!;

        // Validate the board is virtual
        if (match.BoardId.HasValue)
        {
            var board = await dbContext.Boards.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == match.BoardId.Value, cancellationToken);
            if (board is not null && !board.IsVirtual)
                return BadRequest(new { message = "MatchMaker ist nur für virtuelle Boards verfügbar." });
        }

        if (match.FinishedUtc.HasValue)
            return Conflict(new { message = "Match ist bereits beendet." });

        // Extract score data from payload
        bool finished = false;
        if (payload.TryGetProperty("finished", out var finishedEl))
        {
            finished = finishedEl.ValueKind == JsonValueKind.True
                || (finishedEl.ValueKind == JsonValueKind.String && string.Equals(finishedEl.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                || (finishedEl.ValueKind == JsonValueKind.Number && finishedEl.TryGetInt32(out var n) && n != 0);
        }

        var adMatch = new AutodartsMatchDetail(
            Id: matchId.ToString(),
            Variant: payload.TryGetProperty("variant", out var v) ? v.GetString() : null,
            GameMode: payload.TryGetProperty("gameMode", out var gm) ? gm.GetString() : null,
            Finished: finished,
            Players: payload.TryGetProperty("players", out var pl) ? pl : null,
            Turns: payload.TryGetProperty("turns", out var t) ? t : null,
            Legs: payload.TryGetProperty("legs", out var lg) ? lg : null,
            Sets: payload.TryGetProperty("sets", out var st) ? st : null,
            Stats: payload.TryGetProperty("stats", out var s) ? s : null,
            RawJson: payload);

        var participants = await dbContext.Participants
            .AsNoTracking()
            .Where(x => x.Id == match.HomeParticipantId || x.Id == match.AwayParticipantId)
            .ToListAsync(cancellationToken);
        var homeParticipant = participants.FirstOrDefault(x => x.Id == match.HomeParticipantId);
        var awayParticipant = participants.FirstOrDefault(x => x.Id == match.AwayParticipantId);

        var mappedScores = AutodartsMatchScoreMapper.MapScores(
            adMatch,
            homeParticipant?.DisplayName ?? homeParticipant?.AccountName,
            awayParticipant?.DisplayName ?? awayParticipant?.AccountName);

        var result = await matchService.SyncMatchFromExternalAsync(
            matchId,
            mappedScores.HomeLegs,
            mappedScores.AwayLegs,
            mappedScores.HomeSets,
            mappedScores.AwaySets,
            finished,
            cancellationToken);

        if (result is not null)
        {
            // Sync statistics
            var syncResult = await AutodartsMatchStatisticsSyncService.UpsertFromRawAsync(
                dbContext,
                matchId,
                match.HomeParticipantId,
                match.AwayParticipantId,
                payload,
                AutodartsMatchStatisticsSyncService.ResolveSenderUtc(payload, DateTimeOffset.UtcNow),
                match.HomeParticipantName,
                match.AwayParticipantName,
                cancellationToken);

            await NotifyTournamentAsync(result.TournamentId, "MatchUpdated");

            await BroadcastMatchMakerRealtimeAsync(result, payload, mappedScores, finished, syncResult.Changed, cancellationToken);

            if (syncResult.Changed)
            {
                await tournamentHub.Clients.Group($"tournament-{result.TournamentId}").SendAsync(
                    "MatchStatisticsUpdated",
                    new
                    {
                        tournamentId = result.TournamentId,
                        matchId,
                        sourceTimestamp = syncResult.SenderUtc,
                        timestamp = DateTimeOffset.UtcNow
                    },
                    cancellationToken);
            }
        }

        return Ok(new { processed = true });
    }

    private async Task BroadcastMatchMakerRealtimeAsync(
        MatchDto match,
        JsonElement payload,
        AutodartsMappedScoreResult mappedScores,
        bool finished,
        bool statisticsChanged,
        CancellationToken cancellationToken)
    {
        var activePlayerIndex = TryGetFirstTurnInt(payload, "player");
        var round = TryGetFirstTurnInt(payload, "round");
        var turn = TryGetFirstTurnInt(payload, "dart");
        var turnScore = TryGetFirstTurnThrowsScoreSum(payload);
        var turnBusted = TryGetFirstTurnBool(payload, "busted") ?? TryGetBool(payload, "turnBusted");
        var currentTurnId = TryGetFirstTurnString(payload, "id");
        var currentTurnThrowCount = TryGetFirstTurnThrowsCount(payload);
        var sourceTimestamp = TryGetDateTimeOffset(payload, "timestamp");
        var sequence = Interlocked.Increment(ref _matchMakerRealtimeSequence);
        var homePoints = mappedScores.HomePoints ?? TryGetPlayerPoints(payload, 0);
        var awayPoints = mappedScores.AwayPoints ?? TryGetPlayerPoints(payload, 1);

        var livePayload = new
        {
            tournamentId = match.TournamentId,
            matchId = match.Id,
            externalMatchId = match.ExternalMatchId,
            boardId = match.BoardId,
            homeLegs = mappedScores.HomeLegs,
            awayLegs = mappedScores.AwayLegs,
            homeSets = mappedScores.HomeSets,
            awaySets = mappedScores.AwaySets,
            homePoints,
            awayPoints,
            activePlayerIndex,
            activePlayerId = activePlayerIndex?.ToString(),
            round,
            turn,
            turnScore,
            turnBusted,
            currentTurnId,
            currentTurnThrowCount,
            finished,
            statisticsChanged,
            rawJson = payload.GetRawText(),
            sourceTimestamp,
            timestamp = DateTimeOffset.UtcNow,
            sequence
        };

        await tournamentHub.Clients.Group($"tournament-{match.TournamentId}")
            .SendAsync("MatchDataReceived", livePayload, cancellationToken);
    }

    private static int? TryGetFirstTurnInt(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
                continue;

            return TryGetInt(turn, propertyName);
        }

        return null;
    }

    private static bool? TryGetFirstTurnBool(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
                continue;

            return TryGetBool(turn, propertyName);
        }

        return null;
    }

    private static string? TryGetFirstTurnString(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
                continue;

            if (turn.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
                return property.GetString();
        }

        return null;
    }

    private static int TryGetFirstTurnThrowsCount(JsonElement payload)
    {
        if (!payload.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            return 0;

        foreach (var turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetThrowsOrDarts(turn, out var throwsElement) || throwsElement.ValueKind != JsonValueKind.Array)
                return 0;

            return throwsElement.GetArrayLength();
        }

        return 0;
    }

    private static int? TryGetFirstTurnThrowsScoreSum(JsonElement payload)
    {
        if (!payload.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetThrowsOrDarts(turn, out var throwsElement) || throwsElement.ValueKind != JsonValueKind.Array)
                return null;

            var sum = 0;
            foreach (var dart in throwsElement.EnumerateArray())
            {
                if (dart.ValueKind == JsonValueKind.Object)
                    sum += TryGetInt(dart, "score") ?? 0;
            }

            return sum;
        }

        return null;
    }

    private static bool TryGetThrowsOrDarts(JsonElement turn, out JsonElement dartsElement)
    {
        if (turn.TryGetProperty("throws", out dartsElement) && dartsElement.ValueKind == JsonValueKind.Array)
            return true;

        if (turn.TryGetProperty("darts", out dartsElement) && dartsElement.ValueKind == JsonValueKind.Array)
            return true;

        dartsElement = default;
        return false;
    }

    private static int? TryGetPlayerPoints(JsonElement payload, int playerIndex)
    {
        if (!payload.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
            return null;

        var index = 0;
        foreach (var player in players.EnumerateArray())
        {
            if (player.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            if (index == playerIndex)
            {
                return TryGetInt(player, "points") ?? TryGetInt(player, "score");
            }

            index++;
        }

        return null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var asNumber))
            return asNumber;

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var asString))
            return asString;

        return null;
    }

    private static bool? TryGetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.True)
            return true;
        if (property.ValueKind == JsonValueKind.False)
            return false;
        if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var asString))
            return asString;

        return null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        return DateTimeOffset.TryParse(property.GetString(), out var parsed) ? parsed : null;
    }

    private async Task<ActionResult?> RequireManagerAccessAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return ToDeniedResult(await tournamentAuthorization.EnsureManagerOrIntegrationAsync(HttpContext, tournamentId, cancellationToken));
    }

    private async Task<(ActionResult? Denied, MatchDto? Match)> RequireManagerForMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (match is null)
            return (NotFound(), null);

        var denied = await RequireManagerAccessAsync(match.TournamentId, cancellationToken);
        return (denied, match);
    }

    private async Task<(ActionResult? Denied, MatchDto? Match)> RequireSelfOrManagerForMatchAsync(Guid matchId, string userAccountName, CancellationToken cancellationToken)
    {
        var match = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (match is null)
            return (NotFound(), null);

        var access = await tournamentAuthorization.EnsureSelfOrManagerOrIntegrationAsync(HttpContext, match.TournamentId, userAccountName, cancellationToken);
        return (ToDeniedResult(access), match);
    }

    private ActionResult? ToDeniedResult(AccessCheckResult access)
    {
        return access.Allowed
            ? null
            : StatusCode(access.StatusCode, new { message = access.Message });
    }
}