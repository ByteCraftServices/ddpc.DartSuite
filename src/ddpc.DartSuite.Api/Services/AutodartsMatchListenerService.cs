using System.Collections.Concurrent;
using System.Text.Json;
using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.ApiClient.Contracts;
using ddpc.DartSuite.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace ddpc.DartSuite.Api.Services;

/// <summary>
/// Background service that maintains active polling listeners for running Autodarts matches.
/// For each match with an ExternalMatchId that is not finished, a listener polls the Autodarts
/// API every few seconds and pushes updates via SignalR to connected Blazor clients.
/// </summary>
public sealed class AutodartsMatchListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<BoardStatusHub> _hubContext;
    private readonly AutodartsSessionStore _sessionStore;
    private readonly ILogger<AutodartsMatchListenerService> _logger;

    private readonly ConcurrentDictionary<Guid, ActiveMatchListener> _listeners = new();
    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromSeconds(60);

    public AutodartsMatchListenerService(
        IServiceProvider serviceProvider,
        IHubContext<BoardStatusHub> hubContext,
        AutodartsSessionStore sessionStore,
        ILogger<AutodartsMatchListenerService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <summary>Returns the set of DartSuite match IDs that currently have active listeners.</summary>
    public IReadOnlyDictionary<Guid, MatchListenerInfo> GetActiveListeners()
    {
        var result = new Dictionary<Guid, MatchListenerInfo>();
        foreach (var (matchId, listener) in _listeners)
        {
            result[matchId] = new MatchListenerInfo(
                matchId,
                listener.ExternalMatchId,
                listener.BoardId,
                listener.IsRunning,
                listener.LastUpdateUtc,
                listener.LastError);
        }
        return result;
    }

    /// <summary>Ensures a listener exists for the given match. Creates one if missing.</summary>
    public void EnsureListener(Guid matchId, string externalMatchId, Guid? boardId)
    {
        if (_listeners.ContainsKey(matchId)) return;

        var cts = new CancellationTokenSource();
        var listener = new ActiveMatchListener(externalMatchId, boardId, cts);
        if (_listeners.TryAdd(matchId, listener))
        {
            _ = RunListenerAsync(matchId, listener);
            _logger.LogInformation("Listener created for match {MatchId} (external: {ExternalMatchId})", matchId, externalMatchId);
        }
    }

    /// <summary>Stops and removes the listener for the given match.</summary>
    public void StopListener(Guid matchId)
    {
        if (_listeners.TryRemove(matchId, out var listener))
        {
            listener.Cts.Cancel();
            listener.Cts.Dispose();
            _logger.LogInformation("Listener stopped for match {MatchId}", matchId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutodartsMatchListenerService started");

        // Periodically scan for matches that need listeners
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndManageListenersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error during listener scan");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        // Clean up all listeners on shutdown
        foreach (var (matchId, _) in _listeners)
        {
            StopListener(matchId);
        }
    }

    private async Task ScanAndManageListenersAsync(CancellationToken ct)
    {
        // Only scan when we have an active Autodarts session
        var session = _sessionStore.GetActive();
        if (session?.AccessToken is null) return;

        using var scope = _serviceProvider.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardManagementService>();

        var boards = await boardService.GetBoardsAsync(ct);
        var boardsWithTournament = boards.Where(b => b.TournamentId.HasValue).ToList();

        foreach (var board in boardsWithTournament)
        {
            if (!board.TournamentId.HasValue) continue;

            var matches = await matchService.GetMatchesAsync(board.TournamentId.Value, ct);

            // Find matches that need listeners: have ExternalMatchId, not finished
            var activeMatches = matches
                .Where(m => !string.IsNullOrEmpty(m.ExternalMatchId) && m.FinishedUtc is null)
                .ToList();

            foreach (var match in activeMatches)
            {
                EnsureListener(match.Id, match.ExternalMatchId!, match.BoardId);
            }
        }

        // Remove listeners for matches that are finished or no longer have ExternalMatchId
        var toRemove = new List<Guid>();
        foreach (var (matchId, listener) in _listeners)
        {
            using var innerScope = _serviceProvider.CreateScope();
            var svc = innerScope.ServiceProvider.GetRequiredService<IMatchManagementService>();
            var match = await svc.GetMatchAsync(matchId, ct);
            if (match is null || match.FinishedUtc is not null || string.IsNullOrEmpty(match.ExternalMatchId))
            {
                toRemove.Add(matchId);
            }
        }

        foreach (var matchId in toRemove)
        {
            StopListener(matchId);
        }
    }

    private async Task RunListenerAsync(Guid matchId, ActiveMatchListener listener)
    {
        listener.IsRunning = true;
        var ct = listener.Cts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var accessToken = await GetAccessTokenAsync(ct);
                    if (accessToken is null)
                    {
                        listener.LastError = "No Autodarts session";
                        await Task.Delay(PollInterval, ct);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var autodartsClient = scope.ServiceProvider.GetRequiredService<IAutodartsClient>();
                    var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();

                    AutodartsMatchDetail? adMatch;
                    try
                    {
                        adMatch = await autodartsClient.GetMatchAsync(accessToken, listener.ExternalMatchId, ct);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogWarning("[Listener] Match {MatchId}: 401 Unauthorized — attempting token refresh", matchId);

                        // Step 1: Try refresh token
                        var refreshedToken = await ForceRefreshTokenAsync(ct);
                        if (refreshedToken is not null)
                        {
                            try
                            {
                                adMatch = await autodartsClient.GetMatchAsync(refreshedToken, listener.ExternalMatchId, ct);
                                goto matchRetrieved;
                            }
                            catch (HttpRequestException) { /* refreshed token also rejected */ }
                        }

                        // Step 2: Refresh didn't help — full re-login with audience discovery
                        _logger.LogWarning("[Listener] Match {MatchId}: Refresh token still rejected, trying re-login with audience discovery", matchId);
                        var reLoginToken = await ReLoginWithCredentialsAsync(ct);
                        if (reLoginToken is null)
                        {
                            listener.LastError = "Authentication failed";
                            await Task.Delay(PollInterval, ct);
                            continue;
                        }

                        try
                        {
                            adMatch = await autodartsClient.GetMatchAsync(reLoginToken, listener.ExternalMatchId, ct);
                        }
                        catch (HttpRequestException)
                        {
                            listener.LastError = "401 after re-login — check Autodarts credentials";
                            _logger.LogError("[Listener] Match {MatchId}: 401 persists after re-login with all audience candidates", matchId);
                            await Task.Delay(PollInterval, ct);
                            continue;
                        }
                    }
                matchRetrieved:
                    if (adMatch is null)
                    {
                        listener.LastError = "Match not found on Autodarts";
                        _logger.LogWarning("[Listener] Match {MatchId} (ext: {ExternalMatchId}): Autodarts API returned null", matchId, listener.ExternalMatchId);
                        await Task.Delay(PollInterval, ct);
                        continue;
                    }

                    listener.LastError = null;
                    listener.LastUpdateUtc = DateTimeOffset.UtcNow;

                    // Parse legs and update DB
                    var rawJson = adMatch.RawJson;
                    var (homeLegs, awayLegs, homeSets, awaySets) = ParseScoresFromAutodartsMatch(adMatch);
                    var scoreKey = $"{homeSets}:{homeLegs}:{awaySets}:{awayLegs}:{adMatch.Finished}";
                    var isFirstPoll = listener.LastScoreKey is null;
                    var scoreChanged = listener.LastScoreKey != scoreKey;
                    listener.LastScoreKey = scoreKey;

                    // Full response only on first poll or when score changes
                    if (isFirstPoll || scoreChanged)
                    {
                        var reason = isFirstPoll ? "FIRST POLL" : "SCORE CHANGED";
                        _logger.LogInformation(
                            "[Listener] Match {MatchId} (ext: {ExternalMatchId}) — {Reason} — {Home}:{Away}, finished={Finished}",
                            matchId, listener.ExternalMatchId, reason, homeLegs, awayLegs, adMatch.Finished);

                        // Write full JSON to file for inspection (console truncates large JSON)
                        try
                        {
                            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                            Directory.CreateDirectory(logDir);
                            var fileName = $"match-{matchId:N}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
                            var filePath = Path.Combine(logDir, fileName);
                            await File.WriteAllTextAsync(filePath, rawJson.ToString(), ct);
                            _logger.LogInformation("[Listener] Match {MatchId} — API response written to {FilePath}", matchId, filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[Listener] Failed to write API response to file");
                        }
                    }

                    var result = await matchService.SyncMatchFromExternalAsync(
                        matchId, homeLegs, awayLegs, homeSets, awaySets, adMatch.Finished, ct);

                    if (result is not null)
                    {
                        // Push update via SignalR to all connected Blazor clients
                        await _hubContext.Clients.All.SendAsync("MatchUpdated", result, ct);
                        _logger.LogInformation("[Listener] Match {MatchId} — SignalR push: MatchUpdated ({Home}:{Away})", matchId, result.HomeLegs, result.AwayLegs);

                        // Also send raw match data for detailed board view
                        await _hubContext.Clients.All.SendAsync("MatchDataReceived", new
                        {
                            matchId,
                            externalMatchId = listener.ExternalMatchId,
                            boardId = listener.BoardId,
                            homeLegs,
                            awayLegs,
                            homeSets,
                            awaySets,
                            finished = adMatch.Finished,
                            rawJson = adMatch.RawJson.ToString(),
                            timestamp = DateTimeOffset.UtcNow
                        }, ct);
                    }

                    // If match is finished, stop the listener
                    if (adMatch.Finished)
                    {
                        _logger.LogInformation("Match {MatchId} finished, stopping listener", matchId);
                        break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    listener.LastError = ex.Message;
                    _logger.LogWarning(ex, "[Listener] Poll error for match {MatchId} (ext: {ExternalMatchId})", matchId, listener.ExternalMatchId);
                }

                await Task.Delay(PollInterval, ct);
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            listener.IsRunning = false;
            _listeners.TryRemove(matchId, out _);
        }
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        var session = _sessionStore.GetActive();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
            return null;

        // Check if token is still valid with a generous buffer
        if (session.ExpiresAt.HasValue && session.ExpiresAt.Value > DateTimeOffset.UtcNow.Add(TokenRefreshBuffer))
            return session.AccessToken;

        // Token is expired or close to expiry — refresh with lock to prevent concurrent refresh attempts
        return await RefreshTokenWithLockAsync(ct);
    }

    /// <summary>
    /// Thread-safe token refresh using SemaphoreSlim. Only one refresh runs at a time;
    /// concurrent callers wait and then get the already-refreshed token.
    /// This prevents multiple listeners from racing to refresh the same single-use refresh token.
    /// </summary>
    private async Task<string?> RefreshTokenWithLockAsync(CancellationToken ct)
    {
        await _tokenRefreshLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring lock — another thread may have already refreshed
            var session = _sessionStore.GetActive();
            if (session is null) return null;

            if (session.ExpiresAt.HasValue && session.ExpiresAt.Value > DateTimeOffset.UtcNow.Add(TokenRefreshBuffer))
            {
                _logger.LogDebug("[Listener] Token already refreshed by another thread, reusing");
                return session.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                _logger.LogWarning("[Listener] No refresh token available");
                return session.AccessToken;
            }

            using var scope = _serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IAutodartsClient>();
            var newToken = await client.RefreshAccessTokenAsync(session.RefreshToken!, ct);
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(newToken.ExpiresIn > 0 ? newToken.ExpiresIn : 300);
            _sessionStore.UpdateTokens(session.SessionId, newToken.AccessToken, newToken.RefreshToken, expiresAt);
            _logger.LogInformation("[Listener] Token refreshed successfully, new expiry: {ExpiresAt}", expiresAt);
            return newToken.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[Listener] Token refresh failed: {Message}. Attempting re-login with credentials.", ex.Message);
            return await ReLoginWithCredentialsAsync(ct);
        }
        finally
        {
            _tokenRefreshLock.Release();
        }
    }

    /// <summary>
    /// Force-refresh on 401: invalidates the current expiry so RefreshTokenWithLockAsync
    /// will actually refresh instead of reusing the (rejected) token.
    /// </summary>
    private async Task<string?> ForceRefreshTokenAsync(CancellationToken ct)
    {
        var session = _sessionStore.GetActive();
        if (session is null)
            return null;

        // Invalidate current expiry so the lock-based refresh actually runs
        _sessionStore.UpdateTokens(session.SessionId, session.AccessToken!, session.RefreshToken, DateTimeOffset.UtcNow);

        return await RefreshTokenWithLockAsync(ct);
    }

    /// <summary>
    /// Fallback: re-authenticate with stored email/password credentials (like darts-caller).
    /// Tries the stored API audience first, then discovers the correct audience by testing candidates.
    /// This works even when the refresh token has expired.
    /// </summary>
    private async Task<string?> ReLoginWithCredentialsAsync(CancellationToken ct)
    {
        var session = _sessionStore.GetActive();
        if (session is null || string.IsNullOrWhiteSpace(session.UsernameOrEmail) || string.IsNullOrWhiteSpace(session.Password))
        {
            _logger.LogWarning("[Listener] No stored credentials available for re-login");
            return session?.AccessToken;
        }

        using var scope = _serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAutodartsClient>();

        // Build audience list: stored working audience first, then all candidates, then null (no audience)
        var audiencesToTry = new List<string?>();
        if (!string.IsNullOrWhiteSpace(session.ApiAudience))
            audiencesToTry.Add(session.ApiAudience);
        foreach (var candidate in client.GetAudienceCandidates())
            if (!audiencesToTry.Contains(candidate))
                audiencesToTry.Add(candidate);
        audiencesToTry.Add(null); // no audience as last resort

        foreach (var audience in audiencesToTry)
        {
            try
            {
                _logger.LogInformation("[Listener] Trying re-login with audience: {Audience}", audience ?? "(none)");
                var token = await client.AuthenticateAsync(session.UsernameOrEmail, session.Password, audience, ct);
                var expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 300);
                _sessionStore.UpdateTokens(session.SessionId, token.AccessToken, token.RefreshToken, expiresAt);

                if (!string.IsNullOrWhiteSpace(audience))
                    _sessionStore.SetApiAudience(session.SessionId, audience);

                _logger.LogInformation("[Listener] Re-login successful with audience: {Audience}, expiry: {ExpiresAt}", audience ?? "(none)", expiresAt);
                return token.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Listener] Re-login with audience '{Audience}' failed: {Message}", audience ?? "(none)", ex.Message);
            }
        }

        _logger.LogError("[Listener] All re-login attempts failed");
        return session.AccessToken;
    }

    // ─── Leg Parsing (same logic as MatchesController) ───

    private static (int HomeLegs, int AwayLegs, int HomeSets, int AwaySets) ParseScoresFromAutodartsMatch(AutodartsMatchDetail match)
    {
        var rawJson = match.RawJson;
        int p1 = 0, p2 = 0, s1 = 0, s2 = 0;

        if (rawJson.TryGetProperty("legs", out var legsEl) && legsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in legsEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Array)
                    foreach (var leg in item.EnumerateArray()) CountWinner(leg, ref p1, ref p2);
                else if (item.ValueKind == JsonValueKind.Object)
                    CountWinner(item, ref p1, ref p2);
            }
        }

        if (p1 == 0 && p2 == 0 && rawJson.TryGetProperty("sets", out var setsEl) && setsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var set in setsEl.EnumerateArray())
            {
                if (set.ValueKind == JsonValueKind.Object && set.TryGetProperty("legs", out var sl) && sl.ValueKind == JsonValueKind.Array)
                    foreach (var leg in sl.EnumerateArray()) CountWinner(leg, ref p1, ref p2);
            }
        }

        if (p1 == 0 && p2 == 0 && rawJson.TryGetProperty("stats", out var statsEl) && statsEl.ValueKind == JsonValueKind.Array)
        {
            for (int i = 0; i < statsEl.GetArrayLength() && i < 2; i++)
            {
                var v = TryInt(statsEl[i], "legsWon") ?? TryInt(statsEl[i], "legs_won") ?? TryInt(statsEl[i], "legs");
                if (v.HasValue) { if (i == 0) p1 = v.Value; else p2 = v.Value; }
            }
        }

        if (p1 == 0 && p2 == 0 && rawJson.TryGetProperty("gameScores", out var gs) && gs.ValueKind == JsonValueKind.Array)
        {
            for (int i = 0; i < gs.GetArrayLength() && i < 2; i++)
            {
                var v = gs[i].ValueKind == JsonValueKind.Number ? gs[i].GetInt32() : (int?)null;
                if (v.HasValue) { if (i == 0) p1 = v.Value; else p2 = v.Value; }
            }
        }

        // Strategy 5: Parse "scores" array — [{"sets":0,"legs":1}, {"sets":0,"legs":0}]
        if (p1 == 0 && p2 == 0 && rawJson.TryGetProperty("scores", out var scoresEl) && scoresEl.ValueKind == JsonValueKind.Array)
        {
            for (int i = 0; i < scoresEl.GetArrayLength() && i < 2; i++)
            {
                var v = TryInt(scoresEl[i], "legs");
                var sv = TryInt(scoresEl[i], "sets");
                if (v.HasValue) { if (i == 0) p1 = v.Value; else p2 = v.Value; }
                if (sv.HasValue) { if (i == 0) s1 = sv.Value; else s2 = sv.Value; }
            }
        }

        return (p1, p2, s1, s2);
    }

    private static void CountWinner(JsonElement leg, ref int p1, ref int p2)
    {
        var w = TryInt(leg, "winner") ?? TryInt(leg, "won") ?? TryInt(leg, "winnerId");
        if (w.HasValue) { if (w.Value == 0) p1++; else p2++; return; }
        if (leg.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Object)
        {
            w = TryInt(r, "winner") ?? TryInt(r, "won");
            if (w.HasValue) { if (w.Value == 0) p1++; else p2++; }
        }
    }

    private static int? TryInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt32(),
            JsonValueKind.String when int.TryParse(v.GetString(), out var n) => n,
            _ => null
        };
    }

    private sealed class ActiveMatchListener(string externalMatchId, Guid? boardId, CancellationTokenSource cts)
    {
        public string ExternalMatchId { get; } = externalMatchId;
        public Guid? BoardId { get; } = boardId;
        public CancellationTokenSource Cts { get; } = cts;
        public bool IsRunning { get; set; }
        public DateTimeOffset? LastUpdateUtc { get; set; }
        public string? LastError { get; set; }
        public string? LastScoreKey { get; set; }
    }
}

public sealed record MatchListenerInfo(
    Guid MatchId,
    string ExternalMatchId,
    Guid? BoardId,
    bool IsRunning,
    DateTimeOffset? LastUpdateUtc,
    string? LastError);
