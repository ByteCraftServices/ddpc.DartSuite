using System.Collections.Concurrent;
using System.Text.Json;
using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.ApiClient.Contracts;
using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ddpc.DartSuite.Api.Services;

/// <summary>
/// Background service that maintains match monitoring for active Autodarts matches.
/// WebSocket push is preferred and polling is used only as fallback.
/// </summary>
public sealed class AutodartsMatchListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<BoardStatusHub> _hubContext;
    private readonly IHubContext<TournamentHub> _tournamentHubContext;
    private readonly AutodartsSessionStore _sessionStore;
    private readonly ILogger<AutodartsMatchListenerService> _logger;
    private readonly TimeSpan _pollInterval;

    private readonly ConcurrentDictionary<Guid, ActiveMatchListener> _listeners = new();
    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RealtimeLoopDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan RealtimeActivityWindow = TimeSpan.FromSeconds(6);

    public AutodartsMatchListenerService(
        IServiceProvider serviceProvider,
        IHubContext<BoardStatusHub> hubContext,
        IHubContext<TournamentHub> tournamentHubContext,
        AutodartsSessionStore sessionStore,
        IOptions<AutodartsOptions> autodartsOptions,
        ILogger<AutodartsMatchListenerService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _tournamentHubContext = tournamentHubContext;
        _sessionStore = sessionStore;
        _logger = logger;

        var pollMs = Math.Clamp(autodartsOptions.Value.ListenerPollMilliseconds, 200, 5000);
        _pollInterval = TimeSpan.FromMilliseconds(pollMs);
    }

    /// <summary>Returns the set of DartSuite match IDs that currently have active listeners.</summary>
    public IReadOnlyDictionary<Guid, MatchListenerInfo> GetActiveListeners()
    {
        var result = new Dictionary<Guid, MatchListenerInfo>();
        foreach (var (matchId, listener) in _listeners)
        {
            var isWebSocketActive = string.Equals(listener.TransportMode, "websocket", StringComparison.OrdinalIgnoreCase)
                && !listener.IsFallbackActive
                && listener.LastRealtimeEventUtc.HasValue
                && (DateTimeOffset.UtcNow - listener.LastRealtimeEventUtc.Value) <= RealtimeActivityWindow;
            var isPollingActive = listener.IsRunning && !isWebSocketActive;

            result[matchId] = new MatchListenerInfo(
                matchId,
                listener.ExternalMatchId,
                listener.BoardId,
                isPollingActive,
                listener.LastUpdateUtc,
                listener.LastError,
                isWebSocketActive,
                listener.TransportMode,
                listener.IsFallbackActive,
                listener.LastRealtimeEventUtc);
        }
        return result;
    }

    /// <summary>
    /// Ensures monitoring exists for the given match when it is active.
    /// Returns true if monitoring is active after this call.
    /// </summary>
    public async Task<bool> EnsureListenerAsync(Guid matchId, string externalMatchId, Guid? boardId, string? boardExternalId = null, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardManagementService>();

        var match = await matchService.GetMatchAsync(matchId, cancellationToken);
        if (!IsMonitoringEligible(match))
        {
            StopListener(matchId);
            return false;
        }

        var resolvedExternalMatchId = match!.ExternalMatchId!;
        var resolvedBoardId = match.BoardId ?? boardId;

        if (string.IsNullOrWhiteSpace(boardExternalId) && resolvedBoardId.HasValue)
        {
            var board = await boardService.GetBoardAsync(resolvedBoardId.Value, cancellationToken);
            boardExternalId = board?.ExternalBoardId;
        }

        if (string.IsNullOrWhiteSpace(resolvedExternalMatchId))
        {
            _logger.LogWarning("Listener not created for match {MatchId}: external match id is empty", matchId);
            return false;
        }

        if (_listeners.TryGetValue(matchId, out var existingListener))
        {
            if (!string.Equals(existingListener.ExternalMatchId, resolvedExternalMatchId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Listener updated for match {MatchId}: external id {OldExternalMatchId} -> {NewExternalMatchId}",
                    matchId,
                    existingListener.ExternalMatchId,
                    resolvedExternalMatchId);
                existingListener.ExternalMatchId = resolvedExternalMatchId;
            }

            if (resolvedBoardId.HasValue && existingListener.BoardId != resolvedBoardId)
            {
                existingListener.BoardId = resolvedBoardId;
            }

            if (!string.IsNullOrWhiteSpace(boardExternalId)
                && !string.Equals(existingListener.BoardExternalId, boardExternalId, StringComparison.OrdinalIgnoreCase))
            {
                existingListener.BoardExternalId = boardExternalId;
            }

            return true;
        }

        var cts = new CancellationTokenSource();
        var listener = new ActiveMatchListener(resolvedExternalMatchId, resolvedBoardId, boardExternalId, cts);
        if (_listeners.TryAdd(matchId, listener))
        {
            _ = RunListenerAsync(matchId, listener);
            _logger.LogInformation("Listener created for match {MatchId} (external: {ExternalMatchId})", matchId, resolvedExternalMatchId);
        }

        return true;
    }

    /// <summary>Reconciles monitoring for all matches in a tournament.</summary>
    public async Task ReconcileTournamentMonitoringAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardManagementService>();

        var matches = await matchService.GetMatchesAsync(tournamentId, cancellationToken);
        var tournamentMatchIds = matches.Select(m => m.Id).ToHashSet();

        var boards = await boardService.GetBoardsAsync(cancellationToken);
        var boardById = boards
            .Where(b => b.TournamentId == tournamentId)
            .ToDictionary(b => b.Id, b => b);

        var activeMatchIds = new HashSet<Guid>();
        foreach (var match in matches.Where(IsMonitoringEligible))
        {
            activeMatchIds.Add(match.Id);
            var boardExternalId = match.BoardId.HasValue && boardById.TryGetValue(match.BoardId.Value, out var board)
                ? board.ExternalBoardId
                : null;

            await EnsureListenerAsync(match.Id, match.ExternalMatchId!, match.BoardId, boardExternalId, cancellationToken);
        }

        foreach (var listenerMatchId in _listeners.Keys.ToList())
        {
            if (tournamentMatchIds.Contains(listenerMatchId) && !activeMatchIds.Contains(listenerMatchId))
                StopListener(listenerMatchId);
        }
    }

    /// <summary>Reconciles monitoring for all matches assigned to a board.</summary>
    public async Task ReconcileBoardMonitoringAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
        var boardService = scope.ServiceProvider.GetRequiredService<IBoardManagementService>();

        var board = await boardService.GetBoardAsync(boardId, cancellationToken);
        if (board is null || !board.TournamentId.HasValue)
            return;

        var matches = await matchService.GetMatchesAsync(board.TournamentId.Value, cancellationToken);
        var boardMatchIds = matches.Where(m => m.BoardId == boardId).Select(m => m.Id).ToHashSet();

        var activeBoardMatches = matches
            .Where(m => m.BoardId == boardId)
            .Where(IsMonitoringEligible)
            .ToList();

        foreach (var match in activeBoardMatches)
            await EnsureListenerAsync(match.Id, match.ExternalMatchId!, match.BoardId, board.ExternalBoardId, cancellationToken);

        var activeBoardMatchIds = activeBoardMatches.Select(m => m.Id).ToHashSet();
        foreach (var listenerMatchId in _listeners.Keys.ToList())
        {
            if (boardMatchIds.Contains(listenerMatchId) && !activeBoardMatchIds.Contains(listenerMatchId))
                StopListener(listenerMatchId);
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
        _logger.LogInformation("AutodartsMatchListenerService started (pollInterval={PollIntervalMs}ms)", (int)_pollInterval.TotalMilliseconds);

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
        var boardById = boardsWithTournament.ToDictionary(b => b.Id, b => b);

        foreach (var board in boardsWithTournament)
        {
            if (!board.TournamentId.HasValue) continue;

            var matches = await matchService.GetMatchesAsync(board.TournamentId.Value, ct);

            // Match monitoring only applies to active matches.
            var activeMatches = matches
                .Where(IsMonitoringEligible)
                .ToList();

            foreach (var match in activeMatches)
            {
                string? boardExternalId = null;
                if (match.BoardId.HasValue && boardById.TryGetValue(match.BoardId.Value, out var matchedBoard))
                    boardExternalId = matchedBoard.ExternalBoardId;

                await EnsureListenerAsync(match.Id, match.ExternalMatchId!, match.BoardId, boardExternalId, ct);
            }
        }

        // Remove listeners for matches that are finished or no longer have ExternalMatchId
        var toRemove = new List<Guid>();
        foreach (var (matchId, _) in _listeners)
        {
            using var innerScope = _serviceProvider.CreateScope();
            var svc = innerScope.ServiceProvider.GetRequiredService<IMatchManagementService>();
            var match = await svc.GetMatchAsync(matchId, ct);
            if (!IsMonitoringEligible(match))
            {
                toRemove.Add(matchId);
            }
        }

        foreach (var matchId in toRemove)
        {
            StopListener(matchId);
        }
    }

    private static bool IsMonitoringEligible(Application.Contracts.Matches.MatchDto? match)
    {
        if (match is null)
            return false;

        return !string.IsNullOrWhiteSpace(match.ExternalMatchId)
            && match.StartedUtc is not null
            && match.FinishedUtc is null;
    }

    private async Task RunListenerAsync(Guid matchId, ActiveMatchListener listener)
    {
        listener.IsRunning = true;
        var ct = listener.Cts.Token;
        CancellationTokenSource? realtimeCts = null;
        Task? realtimePumpTask = null;

        if (!string.IsNullOrWhiteSpace(listener.BoardExternalId))
        {
            realtimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            realtimePumpTask = RunRealtimePumpAsync(matchId, listener, realtimeCts.Token);
        }

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
                        listener.TransportMode = "polling";
                        listener.IsFallbackActive = true;
                        await Task.Delay(_pollInterval, ct);
                        continue;
                    }

                    var hasRecentRealtimeSignal = !string.IsNullOrWhiteSpace(listener.BoardExternalId)
                        && listener.LastRealtimeEventUtc.HasValue
                        && (DateTimeOffset.UtcNow - listener.LastRealtimeEventUtc.Value) <= RealtimeActivityWindow;

                    if (hasRecentRealtimeSignal)
                    {
                        listener.TransportMode = "websocket";
                        listener.IsFallbackActive = false;

                        if (listener.LastProcessedRealtimeEventUtc.HasValue
                            && listener.LastRealtimeEventUtc <= listener.LastProcessedRealtimeEventUtc)
                        {
                            await Task.Delay(RealtimeLoopDelay, ct);
                            continue;
                        }

                        listener.LastProcessedRealtimeEventUtc = listener.LastRealtimeEventUtc;
                    }
                    else
                    {
                        listener.TransportMode = "polling";
                        listener.IsFallbackActive = true;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var autodartsClient = scope.ServiceProvider.GetRequiredService<IAutodartsClient>();
                    var matchService = scope.ServiceProvider.GetRequiredService<IMatchManagementService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<DartSuiteDbContext>();

                    var websocketEvent = hasRecentRealtimeSignal ? listener.LastRealtimeEvent : null;
                    AutodartsMatchDetail? adMatch = TryBuildMatchDetailFromRealtimeEvent(websocketEvent, listener.ExternalMatchId);

                    if (adMatch is null)
                    {
                        try
                        {
                            adMatch = await autodartsClient.GetMatchAsync(accessToken, listener.ExternalMatchId, false, ct);
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
                                    adMatch = await autodartsClient.GetMatchAsync(refreshedToken, listener.ExternalMatchId, false, ct);
                                    goto matchRetrieved;
                                }
                                catch (HttpRequestException) { /* refreshed token also rejected */ }
                            }

                            // Step 2: Refresh didn't help — full re-login with audience discovery
                            _logger.LogWarning("[Listener] Match {MatchId}: Refresh token still rejected, trying re-login with audience discovery", matchId);
                            var reLoginToken = await ReLoginWithCredentialsAsync(ct);
                            if (reLoginToken is null)
                            {
                                listener.LastError = "Autodarts session expired. Please reconnect in the extension popup.";
                                await Task.Delay(_pollInterval, ct);
                                continue;
                            }

                            try
                            {
                                adMatch = await autodartsClient.GetMatchAsync(reLoginToken, listener.ExternalMatchId, false, ct);
                            }
                            catch (HttpRequestException)
                            {
                                listener.LastError = "Autodarts token unauthorized. Please reconnect in the extension popup.";
                                _logger.LogError("[Listener] Match {MatchId}: 401 persists after re-login with all audience candidates", matchId);
                                await Task.Delay(_pollInterval, ct);
                                continue;
                            }
                        }
                    }
                matchRetrieved:
                    if (adMatch is null)
                    {
                        var persistedMatch = await matchService.GetMatchAsync(matchId, ct);
                        var persistedExternalMatchId = persistedMatch?.ExternalMatchId?.Trim();
                        if (!string.IsNullOrWhiteSpace(persistedExternalMatchId)
                            && !string.Equals(persistedExternalMatchId, listener.ExternalMatchId, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation(
                                "[Listener] Match {MatchId}: switching stale external id from {PreviousExternalMatchId} to persisted {PersistedExternalMatchId}",
                                matchId,
                                listener.ExternalMatchId,
                                persistedExternalMatchId);
                            listener.ExternalMatchId = persistedExternalMatchId;
                            continue;
                        }

                        listener.LastError = "Match not found on Autodarts";
                        _logger.LogWarning("[Listener] Match {MatchId} (ext: {ExternalMatchId}): Autodarts API returned null", matchId, listener.ExternalMatchId);
                        await Task.Delay(_pollInterval, ct);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(adMatch.Id)
                        && !string.Equals(adMatch.Id, listener.ExternalMatchId, StringComparison.OrdinalIgnoreCase))
                    {
                        var previousExternalMatchId = listener.ExternalMatchId;
                        listener.ExternalMatchId = adMatch.Id;

                        var trackedMatch = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, ct);
                        if (trackedMatch is not null
                            && !string.Equals(trackedMatch.ExternalMatchId, adMatch.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            trackedMatch.ExternalMatchId = adMatch.Id;
                            await dbContext.SaveChangesAsync(ct);
                        }

                        _logger.LogInformation(
                            "[Listener] Match {MatchId}: resolved external match id from {PreviousExternalMatchId} to {ResolvedExternalMatchId}",
                            matchId,
                            previousExternalMatchId,
                            adMatch.Id);
                    }

                    listener.LastError = null;
                    listener.LastUpdateUtc = DateTimeOffset.UtcNow;

                    var currentMatch = await matchService.GetMatchAsync(matchId, ct);
                    if (currentMatch is null)
                    {
                        listener.LastError = "Match not found in DartSuite";
                        break;
                    }

                    var participantIds = new[] { currentMatch.HomeParticipantId, currentMatch.AwayParticipantId };
                    var participants = await dbContext.Participants
                        .AsNoTracking()
                        .Where(x => participantIds.Contains(x.Id))
                        .ToListAsync(ct);
                    var homeParticipant = participants.FirstOrDefault(x => x.Id == currentMatch.HomeParticipantId);
                    var awayParticipant = participants.FirstOrDefault(x => x.Id == currentMatch.AwayParticipantId);
                    var homeParticipantName = homeParticipant?.DisplayName ?? homeParticipant?.AccountName;
                    var awayParticipantName = awayParticipant?.DisplayName ?? awayParticipant?.AccountName;

                    var rawJson = adMatch.RawJson;
                    var senderUtc = AutodartsMatchStatisticsSyncService.ResolveSenderUtc(rawJson, DateTimeOffset.UtcNow);
                    var rawJsonText = SafeGetRawJsonText(rawJson);
                    var currentTurnSnapshot = TryGetCurrentTurnSnapshot(rawJson);
                    var externalActivePlayerIndex = TryGetIntProperty(rawJson, "player");
                    var activePlayerId = ResolveActivePlayerId(rawJson, externalActivePlayerIndex);
                    var round = TryGetIntProperty(rawJson, "round");
                    var turn = TryGetIntProperty(rawJson, "turn");
                    var turnScore = TryGetIntProperty(rawJson, "turnScore");
                    var turnBusted = TryGetBoolProperty(rawJson, "turnBusted");
                    var gameScoresKey = GetArrayKey(rawJson, "gameScores");
                    var mappedScores = AutodartsMatchScoreMapper.MapScores(
                        adMatch,
                        homeParticipantName,
                        awayParticipantName);
                    var homeLegs = mappedScores.HomeLegs;
                    var awayLegs = mappedScores.AwayLegs;
                    var homeSets = mappedScores.HomeSets;
                    var awaySets = mappedScores.AwaySets;
                    var activePlayerIndex = MapActivePlayerIndex(externalActivePlayerIndex, mappedScores.HomeSlot, mappedScores.AwaySlot);
                    var resultScoreKey = $"{homeSets}:{homeLegs}:{awaySets}:{awayLegs}:{adMatch.Finished}";
                    var liveScoreKey = $"{resultScoreKey}:{mappedScores.HomePoints?.ToString() ?? "-"}:{mappedScores.AwayPoints?.ToString() ?? "-"}";
                    var realtimeProgressKey =
                        $"{liveScoreKey}:{activePlayerIndex?.ToString() ?? "-"}:{activePlayerId ?? "-"}:{round?.ToString() ?? "-"}:{turn?.ToString() ?? "-"}:{turnScore?.ToString() ?? "-"}:{(turnBusted.HasValue ? (turnBusted.Value ? "1" : "0") : "-")}:{currentTurnSnapshot.Id ?? "-"}:{currentTurnSnapshot.ThrowCount}:{gameScoresKey}";
                    var isFirstPoll = listener.LastScoreKey is null && listener.LastLiveScoreKey is null && listener.LastRealtimeKey is null;
                    var resultScoreChanged = listener.LastScoreKey != resultScoreKey;
                    var liveScoreChanged = listener.LastLiveScoreKey != liveScoreKey;
                    var realtimeChanged = listener.LastRealtimeKey != realtimeProgressKey;
                    listener.LastScoreKey = resultScoreKey;
                    listener.LastLiveScoreKey = liveScoreKey;
                    listener.LastRealtimeKey = realtimeProgressKey;
                    var statisticsChanged = false;

                    // Full response logging on first poll or when live state changes.
                    if (isFirstPoll || realtimeChanged)
                    {
                        var reason = isFirstPoll
                            ? "FIRST POLL"
                            : resultScoreChanged
                                ? "RESULT SCORE CHANGED"
                                : liveScoreChanged
                                    ? "LIVE SCORE CHANGED"
                                    : "TURN/PLAYER CHANGED";
                        _logger.LogInformation(
                            "[Listener] Match {MatchId} (ext: {ExternalMatchId}) — {Reason} — {Home}:{Away}, finished={Finished}, mapping={MappingSource}, apiPlayers={ExternalPlayer1}|{ExternalPlayer2}, homeSlot={HomeSlot}, awaySlot={AwaySlot}, activePlayer={ActivePlayerIndex}, round={Round}, turn={Turn}, turnScore={TurnScore}, throws={ThrowCount}",
                            matchId,
                            listener.ExternalMatchId,
                            reason,
                            homeLegs,
                            awayLegs,
                            adMatch.Finished,
                            mappedScores.MappingSource,
                            mappedScores.ExternalPlayer1,
                            mappedScores.ExternalPlayer2,
                            mappedScores.HomeSlot,
                            mappedScores.AwaySlot,
                            activePlayerIndex,
                            round,
                            turn,
                            turnScore,
                            currentTurnSnapshot.ThrowCount);

                        // Write full JSON to file for inspection (console truncates large JSON)
                        try
                        {
                            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                            Directory.CreateDirectory(logDir);
                            var fileName = $"match-{matchId:N}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
                            var filePath = Path.Combine(logDir, fileName);
                            await File.WriteAllTextAsync(filePath, rawJsonText, ct);
                            _logger.LogInformation("[Listener] Match {MatchId} — API response written to {FilePath}", matchId, filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[Listener] Failed to write API response to file");
                        }
                    }

                    var result = await matchService.SyncMatchFromExternalAsync(
                        matchId, homeLegs, awayLegs, homeSets, awaySets, adMatch.Finished, ct);

                    var shouldSyncStatistics = isFirstPoll || realtimeChanged || adMatch.Finished;

                    if (shouldSyncStatistics)
                    {
                        var syncResult = await AutodartsMatchStatisticsSyncService.UpsertFromRawAsync(
                            dbContext,
                            matchId,
                            currentMatch.HomeParticipantId,
                            currentMatch.AwayParticipantId,
                            adMatch.RawJson,
                            senderUtc,
                            homeParticipantName,
                            awayParticipantName,
                            ct);

                        statisticsChanged = syncResult.Changed;
                        senderUtc = syncResult.SenderUtc;
                        listener.LastStatisticsSyncUtc = syncResult.SenderUtc;
                    }

                    if (result is not null)
                    {
                        // Broadcast errors should not kill the polling loop.
                        if (isFirstPoll || resultScoreChanged || adMatch.Finished)
                            await TryBroadcastMatchUpdatedAsync(matchId, result, senderUtc, ct);

                        if (statisticsChanged)
                            await TryBroadcastMatchStatisticsUpdatedAsync(result.TournamentId, matchId, senderUtc, ct);
                    }

                    if (result is null && statisticsChanged)
                    {
                        await TryBroadcastMatchStatisticsUpdatedAsync(currentMatch.TournamentId, matchId, senderUtc, ct);
                    }

                    if (isFirstPoll || realtimeChanged || adMatch.Finished)
                    {
                        listener.RealtimeSequence++;
                        var eventTimestampUtc = DateTimeOffset.UtcNow;

                        await TryBroadcastRawMatchDataAsync(
                            matchId,
                            currentMatch.TournamentId,
                            senderUtc,
                            eventTimestampUtc,
                            listener.RealtimeSequence,
                            listener,
                            homeLegs,
                            awayLegs,
                            homeSets,
                            awaySets,
                            mappedScores.HomePoints,
                            mappedScores.AwayPoints,
                                activePlayerIndex,
                                activePlayerId,
                                round,
                                turn,
                                turnScore,
                                turnBusted,
                                currentTurnSnapshot.Id,
                                currentTurnSnapshot.ThrowCount,
                            adMatch.Finished,
                            statisticsChanged,
                            rawJsonText,
                            ct);
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
                    listener.LastError = $"{ex.GetType().Name}: {ex.Message}";
                    listener.TransportMode = "polling";
                    listener.IsFallbackActive = true;
                    _logger.LogWarning(ex, "[Listener] Poll error for match {MatchId} (ext: {ExternalMatchId})", matchId, listener.ExternalMatchId);
                }

                var loopDelay = listener.IsFallbackActive ? _pollInterval : RealtimeLoopDelay;
                await Task.Delay(loopDelay, ct);
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            if (realtimeCts is not null)
            {
                realtimeCts.Cancel();
                realtimeCts.Dispose();
            }

            if (realtimePumpTask is not null)
            {
                try
                {
                    await realtimePumpTask;
                }
                catch (OperationCanceledException) { }
            }

            listener.IsRunning = false;
            _listeners.TryRemove(matchId, out _);
        }
    }

    private async Task RunRealtimePumpAsync(Guid matchId, ActiveMatchListener listener, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(listener.BoardExternalId))
            return;

        using var scope = _serviceProvider.CreateScope();
        var autodartsClient = scope.ServiceProvider.GetRequiredService<IAutodartsClient>();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync(ct);
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    listener.TransportMode = "polling";
                    listener.IsFallbackActive = true;
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    continue;
                }

                await foreach (var realtimeEvent in autodartsClient.ReadEventsAsync(accessToken, listener.BoardExternalId, ct))
                {
                    listener.LastRealtimeEvent = realtimeEvent;
                    listener.LastRealtimeEventUtc = DateTimeOffset.UtcNow;
                    listener.TransportMode = "websocket";
                    listener.IsFallbackActive = false;
                }

                listener.TransportMode = "polling";
                listener.IsFallbackActive = true;

                if (!ct.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                listener.TransportMode = "polling";
                listener.IsFallbackActive = true;
                listener.LastError = $"Realtime stream error: {ex.Message}";

                _logger.LogWarning(
                    ex,
                    "[Listener] Realtime stream failed for match {MatchId} boardExternalId={BoardExternalId}; using polling fallback",
                    matchId,
                    listener.BoardExternalId);

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
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
                if (session.ExpiresAt.HasValue && session.ExpiresAt.Value > DateTimeOffset.UtcNow)
                {
                    _logger.LogDebug("[Listener] No refresh token available, reusing current access token until expiry");
                    return session.AccessToken;
                }

                _logger.LogWarning("[Listener] No refresh token available, trying credential re-login");
                var reLoginToken = await ReLoginWithCredentialsAsync(ct);
                if (!string.IsNullOrWhiteSpace(reLoginToken))
                    return reLoginToken;

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
            _logger.LogDebug("[Listener] No stored credentials available for re-login");
            return null;
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
        return null;
    }

    private async Task TryBroadcastMatchUpdatedAsync(Guid matchId, Application.Contracts.Matches.MatchDto result, DateTimeOffset senderUtc, CancellationToken ct)
    {
        try
        {
            await _tournamentHubContext.Clients
                .Group($"tournament-{result.TournamentId}")
                .SendAsync("MatchUpdated", result.TournamentId.ToString(), ct);

            await _tournamentHubContext.Clients
                .Group($"tournament-{result.TournamentId}")
                .SendAsync("MatchUpdatedTimestamped", new
                {
                    tournamentId = result.TournamentId,
                    matchId,
                    timestamp = senderUtc
                }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[Listener] Match {MatchId} — TournamentHub broadcast failed", matchId);
        }

        try
        {
            await _hubContext.Clients.All.SendAsync("MatchUpdated", result, ct);
            _logger.LogInformation("[Listener] Match {MatchId} — SignalR push: MatchUpdated ({Home}:{Away})", matchId, result.HomeLegs, result.AwayLegs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[Listener] Match {MatchId} — BoardStatusHub MatchUpdated broadcast failed", matchId);
        }
    }

    private async Task TryBroadcastRawMatchDataAsync(
        Guid matchId,
        Guid tournamentId,
        DateTimeOffset sourceTimestampUtc,
        DateTimeOffset eventTimestampUtc,
        long sequence,
        ActiveMatchListener listener,
        int homeLegs,
        int awayLegs,
        int homeSets,
        int awaySets,
        int? homePoints,
        int? awayPoints,
        int? activePlayerIndex,
        string? activePlayerId,
        int? round,
        int? turn,
        int? turnScore,
        bool? turnBusted,
        string? currentTurnId,
        int currentTurnThrowCount,
        bool finished,
        bool statisticsChanged,
        string rawJsonText,
        CancellationToken ct)
    {
        var payload = new
        {
            tournamentId,
            matchId,
            externalMatchId = listener.ExternalMatchId,
            boardId = listener.BoardId,
            homeLegs,
            awayLegs,
            homeSets,
            awaySets,
            homePoints,
            awayPoints,
            activePlayerIndex,
            activePlayerId,
            round,
            turn,
            turnScore,
            turnBusted,
            currentTurnId,
            currentTurnThrowCount,
            finished,
            statisticsChanged,
            rawJson = rawJsonText,
            sourceTimestamp = sourceTimestampUtc,
            timestamp = eventTimestampUtc,
            sequence
        };

        try
        {
            await _hubContext.Clients.All.SendAsync("MatchDataReceived", payload, ct);
            await _tournamentHubContext.Clients.Group($"tournament-{tournamentId}").SendAsync("MatchDataReceived", payload, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[Listener] Match {MatchId} — MatchDataReceived broadcast failed", matchId);
        }
    }

    private async Task TryBroadcastMatchStatisticsUpdatedAsync(Guid tournamentId, Guid matchId, DateTimeOffset sourceTimestampUtc, CancellationToken ct)
    {
        var payload = new
        {
            tournamentId,
            matchId,
            sourceTimestamp = sourceTimestampUtc,
            timestamp = DateTimeOffset.UtcNow
        };

        try
        {
            await _tournamentHubContext.Clients.Group($"tournament-{tournamentId}").SendAsync("MatchStatisticsUpdated", payload, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[Listener] Match {MatchId} — MatchStatisticsUpdated broadcast failed", matchId);
        }
    }

    private static AutodartsMatchDetail? TryBuildMatchDetailFromRealtimeEvent(AutodartsEvent? realtimeEvent, string fallbackMatchId)
    {
        if (realtimeEvent is null
            || !string.Equals(realtimeEvent.EventType, "match-state", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(realtimeEvent.PayloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(realtimeEvent.PayloadJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return new AutodartsMatchDetail(
                TryGetStringProperty(root, "id") ?? fallbackMatchId,
                TryGetStringProperty(root, "variant"),
                TryGetStringProperty(root, "gameMode"),
                root.TryGetProperty("finished", out var finishedElement) && finishedElement.ValueKind == JsonValueKind.True,
                root.TryGetProperty("players", out var playersElement) && playersElement.ValueKind != JsonValueKind.Null ? playersElement.Clone() : null,
                root.TryGetProperty("turns", out var turnsElement) && turnsElement.ValueKind != JsonValueKind.Null ? turnsElement.Clone() : null,
                root.TryGetProperty("legs", out var legsElement) && legsElement.ValueKind != JsonValueKind.Null ? legsElement.Clone() : null,
                root.TryGetProperty("sets", out var setsElement) && setsElement.ValueKind != JsonValueKind.Null ? setsElement.Clone() : null,
                root.TryGetProperty("stats", out var statsElement) && statsElement.ValueKind != JsonValueKind.Null ? statsElement.Clone() : null,
                root.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? MapActivePlayerIndex(int? externalActivePlayerIndex, int homeSlot, int awaySlot)
    {
        if (!externalActivePlayerIndex.HasValue)
            return null;

        if (externalActivePlayerIndex.Value == homeSlot)
            return 0;

        if (externalActivePlayerIndex.Value == awaySlot)
            return 1;

        return externalActivePlayerIndex;
    }

    private static string SafeGetRawJsonText(JsonElement rawJson)
    {
        try
        {
            if (rawJson.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return "{}";

            return rawJson.GetRawText();
        }
        catch
        {
            return "{}";
        }
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetIntProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            return parsed;

        return null;
    }

    private static bool? TryGetBoolProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.True)
            return true;
        if (value.ValueKind == JsonValueKind.False)
            return false;

        return null;
    }

    private static string ResolveActivePlayerId(JsonElement rawJson, int? activePlayerIndex)
    {
        if (!activePlayerIndex.HasValue || activePlayerIndex.Value < 0)
            return string.Empty;

        if (!rawJson.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var index = activePlayerIndex.Value;
        if (index >= players.GetArrayLength())
            return string.Empty;

        var player = players[index];
        return player.ValueKind == JsonValueKind.Object && player.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetArrayKey(JsonElement rawJson, string propertyName)
    {
        if (!rawJson.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
            return "-";

        var parts = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var i))
            {
                parts.Add(i.ToString());
                continue;
            }

            parts.Add(item.GetRawText());
        }

        return parts.Count == 0 ? "-" : string.Join(",", parts);
    }

    private static TurnSnapshot TryGetCurrentTurnSnapshot(JsonElement rawJson)
    {
        if (!rawJson.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            return default;

        string? turnId = null;
        var throwCount = 0;

        foreach (var turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
                continue;

            if (turn.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                turnId = idElement.GetString();

            if (turn.TryGetProperty("throws", out var throwsElement) && throwsElement.ValueKind == JsonValueKind.Array)
                throwCount = throwsElement.GetArrayLength();
            else
                throwCount = 0;
        }

        return new TurnSnapshot(turnId, throwCount);
    }

    private sealed class ActiveMatchListener(string externalMatchId, Guid? boardId, string? boardExternalId, CancellationTokenSource cts)
    {
        public string ExternalMatchId { get; set; } = externalMatchId;
        public Guid? BoardId { get; set; } = boardId;
        public string? BoardExternalId { get; set; } = boardExternalId;
        public CancellationTokenSource Cts { get; } = cts;
        public bool IsRunning { get; set; }
        public DateTimeOffset? LastUpdateUtc { get; set; }
        public string? LastError { get; set; }
        public string TransportMode { get; set; } = "polling";
        public bool IsFallbackActive { get; set; } = true;
        public AutodartsEvent? LastRealtimeEvent { get; set; }
        public DateTimeOffset? LastRealtimeEventUtc { get; set; }
        public DateTimeOffset? LastProcessedRealtimeEventUtc { get; set; }
        public string? LastScoreKey { get; set; }
        public string? LastLiveScoreKey { get; set; }
        public string? LastRealtimeKey { get; set; }
        public DateTimeOffset? LastStatisticsSyncUtc { get; set; }
        public long RealtimeSequence { get; set; }
    }

    private readonly record struct TurnSnapshot(string? Id, int ThrowCount);
}

public sealed record MatchListenerInfo(
    Guid MatchId,
    string ExternalMatchId,
    Guid? BoardId,
    bool IsRunning,
    DateTimeOffset? LastUpdateUtc,
    string? LastError,
    bool IsWebSocketActive,
    string TransportMode,
    bool IsFallbackActive,
    DateTimeOffset? LastRealtimeEventUtc);
