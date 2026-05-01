using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Infrastructure.Services;

/// <summary>
/// Scheduling strategy for Online tournament variant (Issue #83).
/// Assigns start times based purely on player availability — no boards.
/// start(M) = max(avail(Home), avail(Away), RoundDateTime(M))
/// A match with IsStartTimeLocked + IsBoardLocked + BoardId set is treated as a
/// hybrid locked slot; the board's occupied intervals are tracked to prevent overlap.
/// </summary>
internal static class OnlineSchedulingStrategy
{
    public static void Schedule(
        IEnumerable<Match> allMatches,
        IReadOnlyDictionary<(MatchPhase Phase, int Round), TournamentRound> roundDefs,
        DateTimeOffset tournamentStartUtc)
    {
        var playerAvail = new Dictionary<Guid, DateTimeOffset>();
        // board -> list of (start, end) occupied intervals for locked matches
        var boardIntervals = new Dictionary<Guid, List<(DateTimeOffset Start, DateTimeOffset End)>>();

        var unfinished = allMatches
            .Where(m => m.Status != MatchStatus.WalkOver)
            .OrderBy(m => GetSchedulePhaseBucket(m.Phase))
            .ThenBy(m => m.Phase == MatchPhase.Knockout ? m.Round : 0)
            .ThenBy(m => m.Round)
            .ThenBy(m => m.MatchNumber)
            .ToList();

        foreach (var match in unfinished)
        {
            var roundDef = roundDefs.GetValueOrDefault((match.Phase, match.Round));
            var duration = EstimateDuration(roundDef);
            var roundDateTime = GetRoundDateTime(match, roundDef, tournamentStartUtc);

            // Skip already active/finished matches — track their intervals only
            if (match.FinishedUtc is not null || match.StartedUtc is not null)
            {
                var existingEnd = (match.PlannedStartUtc?.ToUniversalTime() ?? tournamentStartUtc) + duration;
                UpdatePlayerAvail(playerAvail, match, existingEnd);
                TrackBoardInterval(boardIntervals, match.BoardId, match.PlannedStartUtc?.ToUniversalTime() ?? tournamentStartUtc, existingEnd);
                continue;
            }

            // Fully-locked match: keep start time, only validate board overlap
            if (match.IsStartTimeLocked && match.PlannedStartUtc is not null)
            {
                var lockedStart = match.PlannedStartUtc.Value.ToUniversalTime();
                var lockedEnd = lockedStart + duration;
                if (match.IsBoardLocked && match.BoardId.HasValue)
                    TrackBoardInterval(boardIntervals, match.BoardId, lockedStart, lockedEnd);
                UpdatePlayerAvail(playerAvail, match, lockedEnd);
                continue;
            }

            // Compute earliest start from player availability
            var earliest = roundDateTime;
            if (match.HomeParticipantId != Guid.Empty && playerAvail.TryGetValue(match.HomeParticipantId, out var homeAvail))
                earliest = homeAvail > earliest ? homeAvail : earliest;
            if (match.AwayParticipantId != Guid.Empty && playerAvail.TryGetValue(match.AwayParticipantId, out var awayAvail))
                earliest = awayAvail > earliest ? awayAvail : earliest;

            var matchStart = earliest;
            var matchEnd = matchStart + duration;

            // Hybrid: board is locked — find a slot that doesn't overlap existing board usage
            if (match.IsBoardLocked && match.BoardId.HasValue)
            {
                matchStart = FindNonOverlappingSlot(boardIntervals, match.BoardId.Value, earliest, duration);
                matchEnd = matchStart + duration;
                TrackBoardInterval(boardIntervals, match.BoardId, matchStart, matchEnd);
            }

            match.PlannedStartUtc = matchStart.ToUniversalTime();
            match.PlannedEndUtc = matchEnd.ToUniversalTime();
            match.ExpectedEndUtc = match.PlannedEndUtc;
            match.SchedulingStatus = SchedulingStatus.None;

            UpdatePlayerAvail(playerAvail, match, matchEnd);
        }
    }

    private static DateTimeOffset GetRoundDateTime(
        Match match,
        TournamentRound? roundDef,
        DateTimeOffset tournamentStartUtc)
    {
        if (roundDef?.RoundDate is not null)
        {
            var time = roundDef.RoundStartTime ?? TimeOnly.MinValue;
            var localDt = roundDef.RoundDate.Value.ToDateTime(time);
            localDt = DateTime.SpecifyKind(localDt, DateTimeKind.Unspecified);
            var offset = TimeZoneInfo.Local.GetUtcOffset(localDt);
            return new DateTimeOffset(localDt, offset).ToUniversalTime();
        }

        return tournamentStartUtc;
    }

    private static TimeSpan EstimateDuration(TournamentRound? roundDef)
    {
        var legSecs = roundDef?.LegDurationSeconds > 0 ? roundDef.LegDurationSeconds : 300;
        var totalSecs = (roundDef?.Sets ?? 1) * (roundDef?.Legs ?? 3) * legSecs;
        return TimeSpan.FromSeconds(totalSecs);
    }

    private static void UpdatePlayerAvail(Dictionary<Guid, DateTimeOffset> avail, Match match, DateTimeOffset end)
    {
        if (match.HomeParticipantId != Guid.Empty)
        {
            if (!avail.TryGetValue(match.HomeParticipantId, out var cur) || end > cur)
                avail[match.HomeParticipantId] = end;
        }
        if (match.AwayParticipantId != Guid.Empty)
        {
            if (!avail.TryGetValue(match.AwayParticipantId, out var cur) || end > cur)
                avail[match.AwayParticipantId] = end;
        }
    }

    private static void TrackBoardInterval(
        Dictionary<Guid, List<(DateTimeOffset, DateTimeOffset)>> boardIntervals,
        Guid? boardId,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        if (!boardId.HasValue) return;
        if (!boardIntervals.TryGetValue(boardId.Value, out var intervals))
            boardIntervals[boardId.Value] = intervals = [];
        intervals.Add((start, end));
    }

    private static DateTimeOffset FindNonOverlappingSlot(
        Dictionary<Guid, List<(DateTimeOffset Start, DateTimeOffset End)>> boardIntervals,
        Guid boardId,
        DateTimeOffset earliest,
        TimeSpan duration)
    {
        if (!boardIntervals.TryGetValue(boardId, out var intervals) || intervals.Count == 0)
            return earliest;

        var candidate = earliest;
        bool conflict;
        do
        {
            conflict = false;
            foreach (var (iStart, iEnd) in intervals)
            {
                if (candidate < iEnd && candidate + duration > iStart)
                {
                    candidate = iEnd;
                    conflict = true;
                    break;
                }
            }
        } while (conflict);

        return candidate;
    }

    private static int GetSchedulePhaseBucket(MatchPhase phase) =>
        phase == MatchPhase.Group ? 0 : 1;
}
