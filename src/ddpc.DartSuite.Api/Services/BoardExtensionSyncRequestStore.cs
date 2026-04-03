using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ddpc.DartSuite.Api.Services;

public sealed class BoardExtensionSyncRequestStore(ILogger<BoardExtensionSyncRequestStore> logger)
{
    private static readonly TimeSpan RequestTtl = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<Guid, PendingSyncRequest> _pendingRequests = new();
    private readonly ConcurrentDictionary<Guid, SyncTelemetry> _latestTelemetryByBoard = new();

    public PendingSyncRequest Request(Guid boardId)
    {
        CleanupExpiredRequests();

        var request = new PendingSyncRequest(Guid.NewGuid(), DateTimeOffset.UtcNow);
        _pendingRequests[boardId] = request;

        logger.LogInformation(
            "Extension sync requested board={BoardId} requestId={RequestId} requestedAtUtc={RequestedAtUtc}",
            boardId,
            request.RequestId,
            request.RequestedAtUtc);

        _latestTelemetryByBoard.AddOrUpdate(
            boardId,
            _ => new SyncTelemetry(
                BoardId: boardId,
                RequestId: request.RequestId,
                RequestedAtUtc: request.RequestedAtUtc,
                ConsumedAtUtc: null,
                ShouldSync: null,
                ReportedAtUtc: null,
                Matched: null,
                MatchId: null,
                MatchedBy: null,
                DerivedStatus: null,
                BoardCurrentMatchId: null,
                BoardCurrentMatchLabel: null,
                ExternalMatchId: null,
                Player1: null,
                Player2: null,
                MatchStatus: null,
                SourceUrl: null,
                TournamentId: null),
            (_, existing) => existing with
            {
                RequestId = request.RequestId,
                RequestedAtUtc = request.RequestedAtUtc,
                ConsumedAtUtc = null,
                ShouldSync = null,
                ReportedAtUtc = null,
                Matched = null,
                MatchId = null,
                MatchedBy = null,
                DerivedStatus = null,
                BoardCurrentMatchId = null,
                BoardCurrentMatchLabel = null,
                ExternalMatchId = null,
                Player1 = null,
                Player2 = null,
                MatchStatus = null,
                SourceUrl = null,
                TournamentId = null
            });

        return request;
    }

    public SyncConsumeResult Consume(Guid boardId)
    {
        CleanupExpiredRequests();

        if (_pendingRequests.TryRemove(boardId, out var request))
        {
            logger.LogInformation(
                "Extension sync consumed board={BoardId} requestId={RequestId} requestedAtUtc={RequestedAtUtc}",
                boardId,
                request.RequestId,
                request.RequestedAtUtc);

            var consumedAtUtc = DateTimeOffset.UtcNow;
            _latestTelemetryByBoard.AddOrUpdate(
                boardId,
                _ => new SyncTelemetry(
                    BoardId: boardId,
                    RequestId: request.RequestId,
                    RequestedAtUtc: request.RequestedAtUtc,
                    ConsumedAtUtc: consumedAtUtc,
                    ShouldSync: true,
                    ReportedAtUtc: null,
                    Matched: null,
                    MatchId: null,
                    MatchedBy: null,
                    DerivedStatus: null,
                    BoardCurrentMatchId: null,
                    BoardCurrentMatchLabel: null,
                    ExternalMatchId: null,
                    Player1: null,
                    Player2: null,
                    MatchStatus: null,
                    SourceUrl: null,
                    TournamentId: null),
                (_, existing) => existing with
                {
                    RequestId = request.RequestId,
                    RequestedAtUtc = request.RequestedAtUtc,
                    ConsumedAtUtc = consumedAtUtc,
                    ShouldSync = true
                });

            return new SyncConsumeResult(true, request.RequestId, request.RequestedAtUtc);
        }

        logger.LogDebug("Extension sync consume miss board={BoardId}", boardId);
        _latestTelemetryByBoard.AddOrUpdate(
            boardId,
            _ => new SyncTelemetry(
                BoardId: boardId,
                RequestId: null,
                RequestedAtUtc: null,
                ConsumedAtUtc: DateTimeOffset.UtcNow,
                ShouldSync: false,
                ReportedAtUtc: null,
                Matched: null,
                MatchId: null,
                MatchedBy: null,
                DerivedStatus: null,
                BoardCurrentMatchId: null,
                BoardCurrentMatchLabel: null,
                ExternalMatchId: null,
                Player1: null,
                Player2: null,
                MatchStatus: null,
                SourceUrl: null,
                TournamentId: null),
            (_, existing) => existing with
            {
                ConsumedAtUtc = DateTimeOffset.UtcNow,
                ShouldSync = false
            });
        return new SyncConsumeResult(false, null, null);
    }

    public void SetLatestReport(SyncTelemetry telemetry)
    {
        _latestTelemetryByBoard[telemetry.BoardId] = telemetry;
    }

    public SyncTelemetry? GetLatest(Guid boardId)
        => _latestTelemetryByBoard.TryGetValue(boardId, out var telemetry) ? telemetry : null;

    private void CleanupExpiredRequests()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _pendingRequests)
        {
            if (now - entry.Value.RequestedAtUtc > RequestTtl)
            {
                _pendingRequests.TryRemove(entry.Key, out _);
                logger.LogWarning(
                    "Extension sync request expired board={BoardId} requestId={RequestId} requestedAtUtc={RequestedAtUtc}",
                    entry.Key,
                    entry.Value.RequestId,
                    entry.Value.RequestedAtUtc);
            }
        }
    }

    public sealed record PendingSyncRequest(Guid RequestId, DateTimeOffset RequestedAtUtc);

    public sealed record SyncConsumeResult(bool ShouldSync, Guid? RequestId, DateTimeOffset? RequestedAtUtc);

    public sealed record SyncTelemetry(
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
