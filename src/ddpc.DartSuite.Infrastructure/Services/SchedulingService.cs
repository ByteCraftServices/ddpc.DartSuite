using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class SchedulingService(DartSuiteDbContext dbContext) : ISchedulingService
{
    public async Task<IReadOnlyList<MatchDto>> CalculateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Round)
            .ThenBy(x => x.MatchNumber)
            .ToListAsync(cancellationToken);

        var boards = await dbContext.Boards.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var tournament = await dbContext.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        if (tournament?.StartTime is null || boards.Count == 0)
            return matches.Select(ToDto).ToList();

        var startDate = tournament.StartDate.ToDateTime(tournament.StartTime.Value);
        var startUtc = ConvertLocalWallTimeToUtc(startDate, TimeZoneInfo.Local);
        var roundDefs = await dbContext.TournamentRounds.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        // Simple round-robin board assignment with estimated durations
        var boardSlots = boards.Select(b => startUtc).ToList();
        var unscheduled = matches.Where(m => m.FinishedUtc == null && !m.IsStartTimeLocked).OrderBy(m => m.Round).ThenBy(m => m.MatchNumber).ToList();

        foreach (var match in unscheduled)
        {
            // Find earliest available board slot
            var earliestIdx = 0;
            for (var i = 1; i < boardSlots.Count; i++)
            {
                if (boardSlots[i] < boardSlots[earliestIdx]) earliestIdx = i;
            }

            var roundDef = roundDefs.FirstOrDefault(r => r.RoundNumber == match.Round && r.Phase == match.Phase);
            var estimatedMinutes = EstimateMatchDuration(roundDef?.Sets ?? 1, roundDef?.Legs ?? 3, roundDef?.LegDurationSeconds ?? 0);

            match.PlannedStartUtc = boardSlots[earliestIdx];
            match.PlannedEndUtc = boardSlots[earliestIdx].AddMinutes(estimatedMinutes);
            match.ExpectedEndUtc = match.PlannedEndUtc;
            match.BoardId = match.IsBoardLocked ? match.BoardId : boards[earliestIdx].Id;
            match.SchedulingStatus = SchedulingStatus.None;

            boardSlots[earliestIdx] = match.PlannedEndUtc.Value.AddMinutes(5); // 5 min buffer
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return matches.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<MatchDto>> RecalculateDelaysAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.FinishedUtc == null)
            .OrderBy(x => x.PlannedStartUtc)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var match in matches)
        {
            if (match.PlannedStartUtc.HasValue && match.PlannedEndUtc.HasValue)
            {
                if (match.StartedUtc.HasValue)
                {
                    var elapsed = now - match.StartedUtc.Value;
                    var planned = match.PlannedEndUtc.Value - match.PlannedStartUtc.Value;
                    if (elapsed > planned)
                    {
                        match.DelayMinutes = (int)(elapsed - planned).TotalMinutes;
                        match.SchedulingStatus = SchedulingStatus.Delayed;
                        match.ExpectedEndUtc = now.AddMinutes(5); // conservative estimate
                    }
                    else
                    {
                        match.DelayMinutes = 0;
                        match.SchedulingStatus = elapsed < planned * 0.8 ? SchedulingStatus.Ahead : SchedulingStatus.InTime;
                        match.ExpectedEndUtc = match.StartedUtc.Value + planned;
                    }
                }
                else
                {
                    match.SchedulingStatus = SchedulingStatus.None;
                    match.ExpectedEndUtc = match.PlannedEndUtc;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return matches.Select(ToDto).ToList();
    }

    public async Task<MatchDto?> UpdateMatchTimingAsync(Guid matchId, DateTimeOffset? actualStart, DateTimeOffset? actualEnd, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;

        if (actualStart.HasValue) match.StartedUtc = actualStart.Value.ToUniversalTime();
        if (actualEnd.HasValue) match.FinishedUtc = actualEnd.Value.ToUniversalTime();

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    public async Task<IReadOnlyList<MatchDto>> GetBoardScheduleAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.Matches.AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.PlannedStartUtc)
            .ThenBy(x => x.Round)
            .ToListAsync(cancellationToken);

        return matches.Select(ToDto).ToList();
    }

    private static int EstimateMatchDuration(int sets, int legs, int legDurationSeconds)
    {
        // If LegDurationSeconds is configured, use it; otherwise fall back to 5 min/leg
        var secondsPerLeg = legDurationSeconds > 0 ? legDurationSeconds : 300;
        var totalSeconds = sets * legs * secondsPerLeg;
        return (int)Math.Ceiling(totalSeconds / 60.0);
    }

    private static MatchDto ToDto(Domain.Entities.Match m) => new(
        m.Id, m.TournamentId, m.Phase.ToString(), m.GroupNumber,
        m.Round, m.MatchNumber, m.BoardId,
        m.HomeParticipantId, m.AwayParticipantId,
        m.HomeLegs, m.AwayLegs, m.HomeSets, m.AwaySets, m.WinnerParticipantId,
        m.PlannedStartUtc, m.IsStartTimeLocked, m.IsBoardLocked,
        m.StartedUtc, m.FinishedUtc, m.Status.ToString(), m.ExternalMatchId,
        m.PlannedEndUtc, m.ExpectedEndUtc, m.DelayMinutes, m.SchedulingStatus.ToString(),
        m.HomeSlotOrigin, m.AwaySlotOrigin, m.NextMatchInfo);

    private static DateTimeOffset ConvertLocalWallTimeToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        localDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(localDateTime))
        {
            // Shift forward to the next valid local minute when DST skips a range.
            var probe = localDateTime;
            for (var i = 0; i < 24 * 60 && timeZone.IsInvalidTime(probe); i++)
            {
                probe = probe.AddMinutes(1);
            }
            localDateTime = probe;
        }

        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            var preferredOffset = timeZone.GetAmbiguousTimeOffsets(localDateTime).Max();
            return new DateTimeOffset(localDateTime, preferredOffset).ToUniversalTime();
        }

        try
        {
            var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
            return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
        }
        catch (ArgumentException)
        {
            // Defensive fallback: move to next valid local minute and convert via explicit offset.
            var probe = localDateTime;
            for (var i = 0; i < 24 * 60 && timeZone.IsInvalidTime(probe); i++)
            {
                probe = probe.AddMinutes(1);
            }

            var offset = timeZone.GetUtcOffset(probe);
            return new DateTimeOffset(probe, offset).ToUniversalTime();
        }
    }
}
