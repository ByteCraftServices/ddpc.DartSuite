using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.ApiClient.Contracts;
using ddpc.DartSuite.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(
    IMatchManagementService matchService,
    IAutodartsClient autodartsClient,
    AutodartsSessionStore sessionStore,
    AutodartsMatchListenerService listenerService,
    ILogger<MatchesController> logger) : ControllerBase
{
    [HttpGet("{tournamentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> Get(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GetMatchesAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/generate")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> Generate(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GenerateKnockoutPlanAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/generate-groups")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GenerateGroups(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GenerateGroupPhaseAsync(tournamentId, cancellationToken));
    }

    [HttpGet("{tournamentId:guid}/group-standings")]
    public async Task<ActionResult<IReadOnlyList<GroupStandingDto>>> GroupStandings(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GetGroupStandingsAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/generate-schedule")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GenerateSchedule(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await matchService.GenerateScheduleAsync(tournamentId, cancellationToken));
    }

    [HttpPatch("{matchId:guid}/swap")]
    public async Task<IActionResult> SwapParticipants(Guid matchId, [FromQuery] Guid participantId, [FromQuery] Guid targetParticipantId, CancellationToken cancellationToken)
    {
        await matchService.SwapParticipantsAsync(matchId, participantId, targetParticipantId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{matchId:guid}/board")]
    public async Task<ActionResult<MatchDto>> AssignBoard(Guid matchId, [FromQuery] Guid boardId, CancellationToken cancellationToken)
    {
        var match = await matchService.AssignBoardAsync(matchId, boardId, cancellationToken);
        return match is null ? NotFound() : Ok(match);
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
        var match = await matchService.UpdateMatchScheduleAsync(matchId, startTime, lockTime, boardId, lockBoard, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPatch("{matchId:guid}/lock-time")]
    public async Task<ActionResult<MatchDto>> ToggleTimeLock(Guid matchId, [FromQuery] bool locked, CancellationToken cancellationToken)
    {
        var match = await matchService.ToggleMatchTimeLockAsync(matchId, locked, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPatch("{matchId:guid}/lock-board")]
    public async Task<ActionResult<MatchDto>> ToggleBoardLock(Guid matchId, [FromQuery] bool locked, CancellationToken cancellationToken)
    {
        var match = await matchService.ToggleMatchBoardLockAsync(matchId, locked, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPost("result")]
    public async Task<ActionResult<MatchDto>> ReportResult([FromBody] ReportMatchResultRequest request, CancellationToken cancellationToken)
    {
        var match = await matchService.ReportResultAsync(request, cancellationToken);
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
            adMatch = await autodartsClient.GetMatchAsync(accessToken, existingMatch.ExternalMatchId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Autodarts API error during sync for match {MatchId}", matchId);
            return StatusCode((int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                new { message = $"Autodarts API Fehler: {ex.StatusCode} — {ex.Message}" });
        }

        if (adMatch is null)
            return NotFound(new { message = $"Autodarts match {existingMatch.ExternalMatchId} not found." });

        // Parse legs won per player from the Autodarts match data
        var (homeLegs, awayLegs, homeSets, awaySets) = ParseScoresFromAutodartsMatch(adMatch);

        logger.LogInformation("[SyncExternal] Match {MatchId} (ext: {ExternalMatchId}): {Home}-{Away} (Sets: {HomeSets}-{AwaySets}), finished={Finished}",
            matchId, existingMatch.ExternalMatchId, homeLegs, awayLegs, homeSets, awaySets, adMatch.Finished);

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

        var result = await matchService.SyncMatchFromExternalAsync(matchId, homeLegs, awayLegs, homeSets, awaySets, adMatch.Finished, cancellationToken);
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

    private static (int HomeLegs, int AwayLegs, int HomeSets, int AwaySets) ParseScoresFromAutodartsMatch(AutodartsMatchDetail match)
    {
        var rawJson = match.RawJson;
        int player1Legs = 0, player2Legs = 0, player1Sets = 0, player2Sets = 0;

        // Strategy 1: Parse "legs" array — could be nested (sets > legs) or flat
        if (rawJson.TryGetProperty("legs", out var legsElement) && legsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var setOrLeg in legsElement.EnumerateArray())
            {
                if (setOrLeg.ValueKind == JsonValueKind.Array)
                {
                    // Nested: each entry is a set containing an array of leg objects
                    foreach (var leg in setOrLeg.EnumerateArray())
                    {
                        CountLegWinner(leg, ref player1Legs, ref player2Legs);
                    }
                }
                else if (setOrLeg.ValueKind == JsonValueKind.Object)
                {
                    // Flat: each entry is a leg object directly
                    CountLegWinner(setOrLeg, ref player1Legs, ref player2Legs);
                }
            }
        }

        // Strategy 2: Parse "sets" array which may contain legs-won info
        if (player1Legs == 0 && player2Legs == 0 &&
            rawJson.TryGetProperty("sets", out var setsEl) && setsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var set in setsEl.EnumerateArray())
            {
                if (set.ValueKind != JsonValueKind.Object) continue;
                // Each set may have "legs" nested inside
                if (set.TryGetProperty("legs", out var setLegs) && setLegs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var leg in setLegs.EnumerateArray())
                    {
                        CountLegWinner(leg, ref player1Legs, ref player2Legs);
                    }
                }
            }
        }

        // Strategy 3: Fallback to "stats" which has aggregate counts
        if (player1Legs == 0 && player2Legs == 0 &&
            rawJson.TryGetProperty("stats", out var statsEl) && statsEl.ValueKind == JsonValueKind.Array)
        {
            for (int i = 0; i < statsEl.GetArrayLength() && i < 2; i++)
            {
                var playerStats = statsEl[i];
                var legsWon = TryGetInt(playerStats, "legsWon")
                           ?? TryGetInt(playerStats, "legs_won")
                           ?? TryGetInt(playerStats, "legs");
                if (legsWon.HasValue)
                {
                    if (i == 0) player1Legs = legsWon.Value;
                    else player2Legs = legsWon.Value;
                }
            }
        }

        // Strategy 4: Fallback to "variant" info if it contains score state
        if (player1Legs == 0 && player2Legs == 0 &&
            rawJson.TryGetProperty("gameScores", out var scores) && scores.ValueKind == JsonValueKind.Array)
        {
            for (int i = 0; i < scores.GetArrayLength() && i < 2; i++)
            {
                var score = TryGetInt(scores[i]);
                if (score.HasValue)
                {
                    if (i == 0) player1Legs = score.Value;
                    else player2Legs = score.Value;
                }
            }
        }

        // Strategy 5: Parse "scores" array — [{"sets":0,"legs":1}, {"sets":0,"legs":0}]
        if (player1Legs == 0 && player2Legs == 0 &&
            rawJson.TryGetProperty("scores", out var scoresEl) && scoresEl.ValueKind == JsonValueKind.Array)
        {
            for (int i = 0; i < scoresEl.GetArrayLength() && i < 2; i++)
            {
                var legsWon = TryGetInt(scoresEl[i], "legs");
                var setsWon = TryGetInt(scoresEl[i], "sets");
                if (legsWon.HasValue)
                {
                    if (i == 0) player1Legs = legsWon.Value;
                    else player2Legs = legsWon.Value;
                }
                if (setsWon.HasValue)
                {
                    if (i == 0) player1Sets = setsWon.Value;
                    else player2Sets = setsWon.Value;
                }
            }
        }

        return (player1Legs, player2Legs, player1Sets, player2Sets);
    }

    private static void CountLegWinner(JsonElement leg, ref int player1Legs, ref int player2Legs)
    {
        // Try multiple property names for the winner indicator
        var winner = TryGetInt(leg, "winner") ?? TryGetInt(leg, "won") ?? TryGetInt(leg, "winnerId");
        if (winner.HasValue)
        {
            if (winner.Value == 0) player1Legs++;
            else player2Legs++;
            return;
        }

        // Some formats use a boolean "isPlayer1Winner" or nested result
        if (leg.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
        {
            var resultWinner = TryGetInt(result, "winner") ?? TryGetInt(result, "won");
            if (resultWinner.HasValue)
            {
                if (resultWinner.Value == 0) player1Legs++;
                else player2Legs++;
            }
        }
    }

    private static int? TryGetInt(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.Number => val.GetInt32(),
                JsonValueKind.String when int.TryParse(val.GetString(), out var n) => n,
                _ => null
            };
        }
        return null;
    }

    private static int? TryGetInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.String when int.TryParse(element.GetString(), out var n) => n,
            _ => null
        };
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
            .Select(l => new MatchListenerInfoDto(l.MatchId, l.ExternalMatchId, l.BoardId, l.IsRunning, l.LastUpdateUtc, l.LastError))
            .ToList();
        return Ok(listeners);
    }

    [HttpPost("{matchId:guid}/listener")]
    public async Task<IActionResult> EnsureListener(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (match is null) return NotFound();
        if (string.IsNullOrEmpty(match.ExternalMatchId))
            return BadRequest(new { message = "Match has no ExternalMatchId." });

        listenerService.EnsureListener(matchId, match.ExternalMatchId, match.BoardId);
        return Ok(new { message = "Listener ensured." });
    }

    [HttpDelete("{matchId:guid}/listener")]
    public IActionResult StopListener(Guid matchId)
    {
        listenerService.StopListener(matchId);
        return NoContent();
    }

    [HttpPost("{matchId:guid}/reset")]
    public async Task<ActionResult<MatchDto>> ResetMatch(Guid matchId, CancellationToken cancellationToken)
    {
        // Stop any active listener before resetting
        listenerService.StopListener(matchId);

        var result = await matchService.ResetMatchAsync(matchId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{matchId:guid}")]
    public async Task<ActionResult<MatchDto>> UpdateMatch(Guid matchId, [FromBody] UpdateMatchRequest request, CancellationToken cancellationToken)
    {
        if (matchId != request.MatchId) return BadRequest();
        var result = await matchService.UpdateMatchAsync(request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("batch-reset")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> BatchReset([FromBody] IReadOnlyList<Guid> matchIds, CancellationToken cancellationToken)
    {
        foreach (var id in matchIds)
            listenerService.StopListener(id);

        var result = await matchService.BatchResetMatchesAsync(matchIds, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{tournamentId:guid}/cleanup")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> CleanupStale(Guid tournamentId, [FromQuery] int staleMinutes = 120, CancellationToken cancellationToken = default)
    {
        var result = await matchService.CleanupStaleMatchesAsync(tournamentId, staleMinutes, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{tournamentId:guid}/check-external")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> CheckExternalMatches(Guid tournamentId, CancellationToken cancellationToken)
    {
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
                await autodartsClient.GetMatchAsync(accessToken, m.ExternalMatchId!, cancellationToken);
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
}