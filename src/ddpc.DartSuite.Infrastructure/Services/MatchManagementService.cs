using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class MatchManagementService(DartSuiteDbContext dbContext, IMatchPredictionService predictionService) : IMatchManagementService
{
    private static MatchDto ToDto(Match m) => new(
        m.Id, m.TournamentId, m.Phase.ToString(), m.GroupNumber,
        m.Round, m.MatchNumber, m.BoardId,
        m.HomeParticipantId, m.AwayParticipantId,
        m.HomeLegs, m.AwayLegs, m.HomeSets, m.AwaySets, m.WinnerParticipantId,
        m.PlannedStartUtc, m.IsStartTimeLocked, m.IsBoardLocked,
        m.StartedUtc, m.FinishedUtc, m.ExternalMatchId);

    public async Task<MatchDto?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        return match is null ? null : ToDto(match);
    }

    public async Task<IReadOnlyList<MatchDto>> GetMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Phase).ThenBy(x => x.Round).ThenBy(x => x.MatchNumber)
            .Select(x => new MatchDto(x.Id, x.TournamentId, x.Phase.ToString(), x.GroupNumber,
                x.Round, x.MatchNumber, x.BoardId,
                x.HomeParticipantId, x.AwayParticipantId,
                x.HomeLegs, x.AwayLegs, x.HomeSets, x.AwaySets, x.WinnerParticipantId,
                x.PlannedStartUtc, x.IsStartTimeLocked, x.IsBoardLocked,
                x.StartedUtc, x.FinishedUtc, x.ExternalMatchId))
            .ToListAsync(cancellationToken);
    }

    // ─── KO bracket with byes + seeded crossing ───

    public async Task<IReadOnlyList<MatchDto>> GenerateKnockoutPlanAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var participants = await dbContext.Participants
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Seed)
            .ToListAsync(cancellationToken);

        if (participants.Count < 2) return [];

        // Remove existing KO matches
        var existingKo = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Knockout)
            .ToListAsync(cancellationToken);
        dbContext.Matches.RemoveRange(existingKo);

        // Pad to next power of 2 with byes
        var bracketSize = 1;
        while (bracketSize < participants.Count) bracketSize *= 2;
        var byeCount = bracketSize - participants.Count;

        // Build seeded order — standard bracket crossing
        var seeds = BuildSeededBracketOrder(bracketSize);

        // Map seed positions to participant IDs (or bye sentinel)
        var playerIds = new Guid[bracketSize];
        for (var i = 0; i < bracketSize; i++)
        {
            var seedIndex = seeds[i];
            playerIds[i] = seedIndex < participants.Count ? participants[seedIndex].Id : Match.ByeParticipantId;
        }

        // Generate all rounds of the KO bracket
        var totalRounds = (int)Math.Log2(bracketSize);
        var matchNumber = 1;
        var currentRoundIds = new List<Guid>();

        // Round 1 matches
        for (var i = 0; i < bracketSize; i += 2)
        {
            var match = new Match
            {
                TournamentId = tournamentId,
                Phase = MatchPhase.Knockout,
                Round = 1,
                MatchNumber = matchNumber++,
                HomeParticipantId = playerIds[i],
                AwayParticipantId = playerIds[i + 1]
            };

            // Auto-resolve bye matches
            if (match.IsBye)
            {
                var winner = match.HomeParticipantId == Match.ByeParticipantId
                    ? match.AwayParticipantId : match.HomeParticipantId;
                match.WinnerParticipantId = winner;
                match.FinishedUtc = DateTimeOffset.UtcNow;
            }

            dbContext.Matches.Add(match);
            currentRoundIds.Add(match.Id);
        }

        // Subsequent rounds (placeholders)
        for (var round = 2; round <= totalRounds; round++)
        {
            var matchesInRound = bracketSize / (int)Math.Pow(2, round);
            for (var i = 0; i < matchesInRound; i++)
            {
                var match = new Match
                {
                    TournamentId = tournamentId,
                    Phase = MatchPhase.Knockout,
                    Round = round,
                    MatchNumber = matchNumber++,
                    HomeParticipantId = Guid.Empty,
                    AwayParticipantId = Guid.Empty
                };
                dbContext.Matches.Add(match);
            }
        }

        // Third-place match
        var tournament = await dbContext.Tournaments.FindAsync([tournamentId], cancellationToken);
        if (tournament?.ThirdPlaceMatch == true && totalRounds >= 2)
        {
            dbContext.Matches.Add(new Match
            {
                TournamentId = tournamentId,
                Phase = MatchPhase.Knockout,
                Round = totalRounds + 1, // special round after final
                MatchNumber = matchNumber,
                HomeParticipantId = Guid.Empty,
                AwayParticipantId = Guid.Empty
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Advance winners from bye matches into next round
        await AdvanceByeWinnersAsync(tournamentId, cancellationToken);

        return await GetMatchesAsync(tournamentId, cancellationToken);
    }

    /// <summary>Standard bracket seeding: 1 vs N, 2 vs N-1, etc., crossing halves.</summary>
    private static int[] BuildSeededBracketOrder(int size)
    {
        if (size == 1) return [0];
        if (size == 2) return [0, 1];

        var result = new int[size];
        result[0] = 0;
        result[1] = 1;
        var positions = 2;

        while (positions < size)
        {
            var temp = new int[positions * 2];
            for (var i = 0; i < positions; i++)
            {
                temp[i * 2] = result[i];
                temp[i * 2 + 1] = positions * 2 - 1 - result[i];
            }
            Array.Copy(temp, result, positions * 2);
            positions *= 2;
        }

        return result;
    }

    private async Task AdvanceByeWinnersAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var matches = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Knockout)
            .OrderBy(x => x.Round).ThenBy(x => x.MatchNumber)
            .ToListAsync(cancellationToken);

        var round1 = matches.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();
        var round2 = matches.Where(m => m.Round == 2).OrderBy(m => m.MatchNumber).ToList();

        for (var i = 0; i < round1.Count && i / 2 < round2.Count; i += 2)
        {
            var m1 = round1[i];
            var m2 = i + 1 < round1.Count ? round1[i + 1] : null;
            var nextMatch = round2[i / 2];

            if (m1.WinnerParticipantId.HasValue && m1.WinnerParticipantId != Guid.Empty)
                nextMatch.HomeParticipantId = m1.WinnerParticipantId.Value;
            if (m2?.WinnerParticipantId.HasValue == true && m2.WinnerParticipantId != Guid.Empty)
                nextMatch.AwayParticipantId = m2.WinnerParticipantId.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Group Phase Generation ───

    public async Task<IReadOnlyList<MatchDto>> GenerateGroupPhaseAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FindAsync([tournamentId], cancellationToken);
        if (tournament is null || tournament.GroupCount < 1) return [];

        var participants = await dbContext.Participants
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Seed)
            .ToListAsync(cancellationToken);

        if (participants.Count < 2) return [];

        // Remove existing group matches
        var existingGroup = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Group)
            .ToListAsync(cancellationToken);
        dbContext.Matches.RemoveRange(existingGroup);

        // Distribute participants into groups based on draw mode
        var groups = DistributeIntoGroups(participants, tournament.GroupCount, tournament.GroupDrawMode);

        // Assign group numbers to participants
        for (var g = 0; g < groups.Count; g++)
            foreach (var p in groups[g])
                p.GroupNumber = g + 1;

        // Generate round-robin matches per group using circle method
        var matchNumber = 1;
        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var roundRobinRounds = GenerateRoundRobinRounds(group);

            for (var repetition = 0; repetition < tournament.MatchesPerOpponent; repetition++)
            {
                for (var r = 0; r < roundRobinRounds.Count; r++)
                {
                    var roundNumber = repetition * roundRobinRounds.Count + r + 1;
                    foreach (var (home, away) in roundRobinRounds[r])
                    {
                        dbContext.Matches.Add(new Match
                        {
                            TournamentId = tournamentId,
                            Phase = MatchPhase.Group,
                            GroupNumber = g + 1,
                            Round = roundNumber,
                            MatchNumber = matchNumber++,
                            HomeParticipantId = home.Id,
                            AwayParticipantId = away.Id
                        });
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetMatchesAsync(tournamentId, cancellationToken);
    }

    private static List<List<Participant>> DistributeIntoGroups(List<Participant> participants, int groupCount, GroupDrawMode drawMode)
    {
        var groups = Enumerable.Range(0, groupCount).Select(_ => new List<Participant>()).ToList();

        var ordered = drawMode switch
        {
            GroupDrawMode.Random => participants.OrderBy(_ => Random.Shared.Next()).ToList(),
            GroupDrawMode.SeededPots => participants, // already sorted by seed
            _ => participants
        };

        // Snake distribution (1→N, N→1, 1→N, ...)
        var forward = true;
        var groupIndex = 0;
        foreach (var p in ordered)
        {
            groups[groupIndex].Add(p);
            if (forward)
            {
                groupIndex++;
                if (groupIndex >= groupCount) { groupIndex = groupCount - 1; forward = false; }
            }
            else
            {
                groupIndex--;
                if (groupIndex < 0) { groupIndex = 0; forward = true; }
            }
        }

        return groups;
    }

    /// <summary>Circle method: n-1 rounds for even n, n rounds for odd n (one player sits out each round).</summary>
    private static List<List<(Participant Home, Participant Away)>> GenerateRoundRobinRounds(List<Participant> group)
    {
        var players = new List<Participant>(group);
        var isOdd = players.Count % 2 != 0;
        if (isOdd) players.Add(null!); // phantom bye player

        var n = players.Count;
        var totalRounds = n - 1;
        var rounds = new List<List<(Participant Home, Participant Away)>>();

        // Fix first player, rotate the rest
        for (var r = 0; r < totalRounds; r++)
        {
            var roundPairs = new List<(Participant, Participant)>();
            for (var i = 0; i < n / 2; i++)
            {
                var home = players[i];
                var away = players[n - 1 - i];
                if (home is not null && away is not null)
                    roundPairs.Add((home, away));
            }
            rounds.Add(roundPairs);

            // Rotate all players except the first one
            var last = players[n - 1];
            for (var i = n - 1; i > 1; i--)
                players[i] = players[i - 1];
            players[1] = last;
        }

        return rounds;
    }

    // ─── Group Standings ───

    public async Task<IReadOnlyList<GroupStandingDto>> GetGroupStandingsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null) return [];

        var participants = await dbContext.Participants.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.GroupNumber != null)
            .ToListAsync(cancellationToken);

        var groupMatches = await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Group && x.FinishedUtc != null)
            .ToListAsync(cancellationToken);

        var standings = new List<GroupStandingDto>();
        foreach (var p in participants)
        {
            var played = groupMatches.Count(m => m.HomeParticipantId == p.Id || m.AwayParticipantId == p.Id);
            var won = groupMatches.Count(m => m.WinnerParticipantId == p.Id);
            var lost = played - won;
            var legsWon = groupMatches.Where(m => m.HomeParticipantId == p.Id).Sum(m => m.HomeLegs)
                        + groupMatches.Where(m => m.AwayParticipantId == p.Id).Sum(m => m.AwayLegs);
            var legsLost = groupMatches.Where(m => m.HomeParticipantId == p.Id).Sum(m => m.AwayLegs)
                         + groupMatches.Where(m => m.AwayParticipantId == p.Id).Sum(m => m.HomeLegs);

            standings.Add(new GroupStandingDto(
                p.Id, p.DisplayName, p.GroupNumber ?? 0,
                played, won, lost,
                won * tournament.WinPoints + legsWon * tournament.LegFactor,
                legsWon, legsLost, legsWon - legsLost));
        }

        return standings.OrderBy(s => s.GroupNumber).ThenByDescending(s => s.Points).ThenByDescending(s => s.LegDifference).ToList();
    }

    // ─── Result Reporting ───

    public async Task<MatchDto?> ReportResultAsync(ReportMatchResultRequest request, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == request.MatchId, cancellationToken);
        if (match is null) return null;

        match.ReportResult(request.HomeLegs, request.AwayLegs, request.HomeSets, request.AwaySets);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Advance winner to next round in KO
        if (match.Phase == MatchPhase.Knockout && match.WinnerParticipantId.HasValue)
            await AdvanceWinnerAsync(match, cancellationToken);

        return ToDto(match);
    }

    private async Task AdvanceWinnerAsync(Match finishedMatch, CancellationToken cancellationToken)
    {
        var allMatches = await dbContext.Matches
            .Where(x => x.TournamentId == finishedMatch.TournamentId && x.Phase == MatchPhase.Knockout)
            .OrderBy(x => x.Round).ThenBy(x => x.MatchNumber)
            .ToListAsync(cancellationToken);

        var currentRound = allMatches.Where(m => m.Round == finishedMatch.Round).OrderBy(m => m.MatchNumber).ToList();
        var nextRound = allMatches.Where(m => m.Round == finishedMatch.Round + 1).OrderBy(m => m.MatchNumber).ToList();

        if (nextRound.Count == 0) return;

        var matchIndex = currentRound.IndexOf(finishedMatch);
        var nextMatchIndex = matchIndex / 2;
        if (nextMatchIndex >= nextRound.Count) return;

        var nextMatch = nextRound[nextMatchIndex];
        if (matchIndex % 2 == 0)
            nextMatch.HomeParticipantId = finishedMatch.WinnerParticipantId!.Value;
        else
            nextMatch.AwayParticipantId = finishedMatch.WinnerParticipantId!.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Board + Participant Operations ───

    public async Task<MatchDto?> AssignBoardAsync(Guid matchId, Guid boardId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;
        match.BoardId = boardId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    public async Task<MatchDto?> SwapParticipantsAsync(Guid matchId, Guid participantId, Guid newParticipantId, CancellationToken cancellationToken = default)
    {
        var tournamentId = await dbContext.Matches.Where(m => m.Id == matchId).Select(m => m.TournamentId).FirstOrDefaultAsync(cancellationToken);
        var allMatches = await dbContext.Matches.Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var targetMatch = allMatches.FirstOrDefault(m => m.Id == matchId);
        if (targetMatch is null) return null;

        // Find the match containing newParticipantId
        var sourceMatch = allMatches.FirstOrDefault(m =>
            m.HomeParticipantId == newParticipantId || m.AwayParticipantId == newParticipantId);

        if (sourceMatch is not null)
        {
            // Swap: put participantId where newParticipantId was
            if (sourceMatch.HomeParticipantId == newParticipantId)
                sourceMatch.HomeParticipantId = participantId;
            else
                sourceMatch.AwayParticipantId = participantId;
        }

        // Put newParticipantId where participantId was
        if (targetMatch.HomeParticipantId == participantId)
            targetMatch.HomeParticipantId = newParticipantId;
        else if (targetMatch.AwayParticipantId == participantId)
            targetMatch.AwayParticipantId = newParticipantId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(targetMatch);
    }

    // ─── Schedule Generation ───

    public async Task<IReadOnlyList<MatchDto>> GenerateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FindAsync([tournamentId], cancellationToken);
        if (tournament?.StartTime is null) return await GetMatchesAsync(tournamentId, cancellationToken);

        var rounds = await dbContext.TournamentRounds.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToDictionaryAsync(x => (x.Phase, x.RoundNumber), cancellationToken);

        var matches = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Phase).ThenBy(x => x.Round).ThenBy(x => x.MatchNumber)
            .ToListAsync(cancellationToken);

        var boards = await dbContext.Boards.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId || x.TournamentId == null)
            .ToListAsync(cancellationToken);

        var startDateTime = tournament.StartDate.ToDateTime(tournament.StartTime.Value);
        var startUtc = new DateTimeOffset(startDateTime, TimeZoneInfo.Local.GetUtcOffset(startDateTime));

        // Simple sequential scheduling: assign times based on round settings
        var boardEndTimes = boards.ToDictionary(b => b.Id, _ => startUtc);
        var playerLastEnd = new Dictionary<Guid, DateTimeOffset>();

        foreach (var match in matches.Where(m => !m.IsBye))
        {
            // Skip finished or started matches — don't replan them
            if (match.FinishedUtc is not null || match.StartedUtc is not null)
            {
                // Still track their board/player end times for subsequent scheduling
                var roundSettingsExisting = rounds.GetValueOrDefault((match.Phase, match.Round));
                var durationExisting = TimeSpan.FromMinutes(roundSettingsExisting?.MatchDurationMinutes ?? 10);
                var endExisting = (match.PlannedStartUtc ?? startUtc) + durationExisting;
                if (match.BoardId.HasValue && boardEndTimes.ContainsKey(match.BoardId.Value))
                    boardEndTimes[match.BoardId.Value] = endExisting > boardEndTimes[match.BoardId.Value] ? endExisting : boardEndTimes[match.BoardId.Value];
                if (match.HomeParticipantId != Guid.Empty) playerLastEnd[match.HomeParticipantId] = endExisting;
                if (match.AwayParticipantId != Guid.Empty) playerLastEnd[match.AwayParticipantId] = endExisting;
                continue;
            }

            var roundSettings = rounds.GetValueOrDefault((match.Phase, match.Round));
            var duration = TimeSpan.FromMinutes(roundSettings?.MatchDurationMinutes ?? 10);
            var pause = TimeSpan.FromMinutes(roundSettings?.PauseBetweenMatchesMinutes ?? 2);
            var minPlayerPause = TimeSpan.FromMinutes(roundSettings?.MinPlayerPauseMinutes ?? 0);

            var earliest = startUtc;

            // Consider player pause constraints
            if (match.HomeParticipantId != Guid.Empty && playerLastEnd.TryGetValue(match.HomeParticipantId, out var homeEnd))
                earliest = homeEnd + minPlayerPause > earliest ? homeEnd + minPlayerPause : earliest;
            if (match.AwayParticipantId != Guid.Empty && playerLastEnd.TryGetValue(match.AwayParticipantId, out var awayEnd))
                earliest = awayEnd + minPlayerPause > earliest ? awayEnd + minPlayerPause : earliest;

            // If start time is locked, keep it; only assign board
            if (match.IsStartTimeLocked && match.PlannedStartUtc is not null)
            {
                var lockedStart = match.PlannedStartUtc.Value;

                // Board assignment: respect locked boards
                if (!match.IsBoardLocked || match.BoardId is null)
                {
                    Guid? lockedBestBoard = null;
                    var lockedBestTime = DateTimeOffset.MaxValue;
                    if (roundSettings?.BoardAssignment == BoardAssignmentMode.Fixed && roundSettings.FixedBoardId.HasValue)
                    {
                        lockedBestBoard = roundSettings.FixedBoardId.Value;
                    }
                    else
                    {
                        foreach (var (boardId, endTime) in boardEndTimes)
                        {
                            if (endTime <= lockedStart && endTime < lockedBestTime)
                            { lockedBestTime = endTime; lockedBestBoard = boardId; }
                        }
                        // If no board is free at locked time, pick earliest available
                        if (lockedBestBoard is null)
                        {
                            foreach (var (boardId, endTime) in boardEndTimes)
                            {
                                if (endTime < lockedBestTime) { lockedBestTime = endTime; lockedBestBoard = boardId; }
                            }
                        }
                    }
                    if (lockedBestBoard.HasValue)
                    {
                        match.BoardId ??= lockedBestBoard;
                        boardEndTimes[match.BoardId!.Value] = lockedStart + duration + pause;
                    }
                }
                else if (match.BoardId.HasValue && boardEndTimes.ContainsKey(match.BoardId.Value))
                {
                    boardEndTimes[match.BoardId.Value] = lockedStart + duration + pause;
                }

                var lockedEnd = lockedStart + duration;
                if (match.HomeParticipantId != Guid.Empty) playerLastEnd[match.HomeParticipantId] = lockedEnd;
                if (match.AwayParticipantId != Guid.Empty) playerLastEnd[match.AwayParticipantId] = lockedEnd;
                continue;
            }

            // Find earliest available board
            Guid? bestBoard = null;
            var bestTime = DateTimeOffset.MaxValue;

            // Respect locked board — don't reassign
            if (match.IsBoardLocked && match.BoardId.HasValue)
            {
                bestBoard = match.BoardId;
                bestTime = boardEndTimes.GetValueOrDefault(match.BoardId.Value, startUtc);
            }
            // Use fixed board from round settings if specified
            else if (roundSettings?.BoardAssignment == BoardAssignmentMode.Fixed && roundSettings.FixedBoardId.HasValue)
            {
                bestBoard = roundSettings.FixedBoardId.Value;
                bestTime = boardEndTimes.GetValueOrDefault(bestBoard.Value, startUtc);
            }
            else if (match.BoardId.HasValue)
            {
                bestBoard = match.BoardId;
                bestTime = boardEndTimes.GetValueOrDefault(match.BoardId.Value, startUtc);
            }
            else
            {
                foreach (var (boardId, endTime) in boardEndTimes)
                {
                    if (endTime < bestTime) { bestTime = endTime; bestBoard = boardId; }
                }
            }

            var matchStart = bestTime > earliest ? bestTime : earliest;
            match.PlannedStartUtc = matchStart;
            if (bestBoard.HasValue)
            {
                match.BoardId ??= bestBoard;
                boardEndTimes[bestBoard.Value] = matchStart + duration + pause;
            }

            var matchEnd = matchStart + duration;
            if (match.HomeParticipantId != Guid.Empty) playerLastEnd[match.HomeParticipantId] = matchEnd;
            if (match.AwayParticipantId != Guid.Empty) playerLastEnd[match.AwayParticipantId] = matchEnd;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetMatchesAsync(tournamentId, cancellationToken);
    }

    // ─── Prediction ───

    public async Task<MatchDto?> UpdateMatchScheduleAsync(Guid matchId, DateTimeOffset? startTime, bool lockTime, Guid? boardId, bool lockBoard, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;

        match.PlannedStartUtc = startTime;
        match.IsStartTimeLocked = lockTime;
        if (boardId.HasValue) match.BoardId = boardId;
        match.IsBoardLocked = lockBoard;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    public async Task<MatchDto?> ToggleMatchTimeLockAsync(Guid matchId, bool locked, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;
        match.IsStartTimeLocked = locked;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    public async Task<MatchDto?> ToggleMatchBoardLockAsync(Guid matchId, bool locked, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;
        match.IsBoardLocked = locked;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    // ─── External Sync ───

    public async Task<MatchDto?> SyncMatchFromExternalAsync(Guid matchId, int homeLegs, int awayLegs, int homeSets, int awaySets, bool finished, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;

        match.HomeLegs = homeLegs;
        match.AwayLegs = awayLegs;
        match.HomeSets = homeSets;
        match.AwaySets = awaySets;

        if (finished && match.FinishedUtc is null)
        {
            match.ReportResult(homeLegs, awayLegs, homeSets, awaySets);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (match.Phase == MatchPhase.Knockout && match.WinnerParticipantId.HasValue)
                await AdvanceWinnerAsync(match, cancellationToken);
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(match);
    }

    public async Task<MatchDto?> ResetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;

        match.HomeLegs = 0;
        match.AwayLegs = 0;
        match.HomeSets = 0;
        match.AwaySets = 0;
        match.WinnerParticipantId = null;
        match.StartedUtc = null;
        match.FinishedUtc = null;
        match.ExternalMatchId = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    // ─── Prediction (original) ───

    public MatchPredictionDto GetPrediction(int targetLegs, int homeLegs, int awayLegs, int homeScore, int awayScore, TimeSpan elapsed)
    {
        var prediction = predictionService.Predict(targetLegs, homeLegs, awayLegs, homeScore, awayScore, elapsed);
        return new MatchPredictionDto(
            prediction.HomeWinProbability,
            prediction.AwayWinProbability,
            Math.Max(1, (int)Math.Round(prediction.EstimatedRemainingDuration.TotalMinutes)),
            prediction.ExpectedResult);
    }
}