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
    private static IQueryable<Participant> ApplyPlanParticipantFilter(IQueryable<Participant> query, Tournament tournament)
        => tournament.TeamplayEnabled
            ? query.Where(x => x.Type == ParticipantType.TeamMember)
            : query;

    private static MatchDto ToDto(Match m) => new(
        m.Id, m.TournamentId, m.Phase.ToString(), m.GroupNumber,
        m.Round, m.MatchNumber, m.BoardId,
        m.HomeParticipantId, m.AwayParticipantId,
        m.HomeLegs, m.AwayLegs, m.HomeSets, m.AwaySets, m.WinnerParticipantId,
        m.PlannedStartUtc, m.IsStartTimeLocked, m.IsBoardLocked,
        m.StartedUtc, m.FinishedUtc, m.Status.ToString(), m.ExternalMatchId,
        m.PlannedEndUtc, m.ExpectedEndUtc, m.DelayMinutes, m.SchedulingStatus.ToString(),
        m.HomeSlotOrigin, m.AwaySlotOrigin, m.NextMatchInfo);

    private async Task EnsureTournamentStructureEditableAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        if (tournament is null)
            throw new InvalidOperationException("Turnier nicht gefunden.");

        if (tournament.IsLocked)
            throw new InvalidOperationException("Das Turnier ist gesperrt. Bitte entsperren Sie es, bevor Sie Änderungen vornehmen.");

        var hasProgressedMatches = await dbContext.Matches.AsNoTracking().AnyAsync(
            x => x.TournamentId == tournamentId
                && x.Status != MatchStatus.WalkOver
                && (x.StartedUtc != null || x.FinishedUtc != null),
            cancellationToken);

        if (hasProgressedMatches)
            throw new InvalidOperationException("Die Turnierstruktur ist gesperrt, da bereits Matches gestartet oder beendet wurden.");
    }

    public async Task<MatchDto?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        return match is null ? null : ToDto(match);
    }

    public async Task<IReadOnlyList<MatchDto>> GetMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var rawMatches = await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Phase).ThenBy(x => x.Round).ThenBy(x => x.MatchNumber)
            .ToListAsync(cancellationToken);

        return rawMatches
            .Select(ToDto)
            .Select(match => ApplyRuntimeSchedulingStatus(match, now))
            .ToList();
    }

    private static MatchDto ApplyRuntimeSchedulingStatus(MatchDto match, DateTimeOffset now)
    {
        if (match.FinishedUtc is not null || match.PlannedStartUtc is null)
            return match;

        if (match.StartedUtc is null && now > match.PlannedStartUtc.Value)
        {
            var runtimeDelayMinutes = Math.Max(0, (int)(now - match.PlannedStartUtc.Value).TotalMinutes);
            return match with
            {
                DelayMinutes = runtimeDelayMinutes,
                SchedulingStatus = "Delayed"
            };
        }

        return match;
    }

    // ─── KO bracket with byes + seeded crossing ───

    public async Task<IReadOnlyList<MatchDto>> GenerateKnockoutPlanAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FindAsync([tournamentId], cancellationToken);
        if (tournament is null) return [];

        await EnsureTournamentStructureEditableAsync(tournamentId, cancellationToken);

        var allParticipants = await ApplyPlanParticipantFilter(
            dbContext.Participants.Where(x => x.TournamentId == tournamentId),
            tournament)
            .ToListAsync(cancellationToken);

        var hasExplicitSeeds = allParticipants.Any(p => p.Seed > 0);
        List<Participant> participants;

        if (tournament.SeedingEnabled)
        {
            var ranked = allParticipants
                .Where(p => p.Seed > 0 && p.Seed <= tournament.SeedTopCount)
                .OrderBy(p => p.Seed)
                .ToList();
            var unranked = allParticipants
                .Except(ranked)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
            participants = ranked.Concat(unranked).ToList();
        }
        else if (tournament.GroupDrawMode == GroupDrawMode.Manual && hasExplicitSeeds)
        {
            var ranked = allParticipants.Where(p => p.Seed > 0).OrderBy(p => p.Seed).ToList();
            var unranked = allParticipants.Except(ranked).OrderBy(_ => Random.Shared.Next()).ToList();
            participants = ranked.Concat(unranked).ToList();
        }
        else
        {
            participants = allParticipants.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        if (participants.Count < 2) return [];

        // Remove existing KO matches
        var existingKo = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Knockout)
            .ToListAsync(cancellationToken);

        // Clear board references to deleted matches
        var deletedIds = existingKo.Select(m => m.Id).ToHashSet();
        var staleBoards = await dbContext.Boards
            .Where(b => b.CurrentMatchId != null && deletedIds.Contains(b.CurrentMatchId.Value))
            .ToListAsync(cancellationToken);
        foreach (var board in staleBoards)
        {
            board.CurrentMatchId = null;
            board.CurrentMatchLabel = null;
            board.UpdatedUtc = DateTimeOffset.UtcNow;
        }

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
            if (match.HomeParticipantId == Match.ByeParticipantId || match.AwayParticipantId == Match.ByeParticipantId)
            {
                var winner = match.HomeParticipantId == Match.ByeParticipantId
                    ? match.AwayParticipantId : match.HomeParticipantId;
                match.WinnerParticipantId = winner;
                match.FinishedUtc = DateTimeOffset.UtcNow;
                match.Status = MatchStatus.WalkOver;
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

        // Auto-transition: plan created → Geplant
        await AutoAdvanceTournamentStatusAsync(tournamentId, cancellationToken);

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

        await EnsureTournamentStructureEditableAsync(tournamentId, cancellationToken);

        var participants = await ApplyPlanParticipantFilter(
            dbContext.Participants.Where(x => x.TournamentId == tournamentId),
            tournament)
            .OrderBy(x => x.Seed)
            .ToListAsync(cancellationToken);

        if (participants.Count < 2) return [];

        // Remove existing group matches
        var existingGroup = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Group)
            .ToListAsync(cancellationToken);

        // Clear board references to deleted matches
        var deletedGroupIds = existingGroup.Select(m => m.Id).ToHashSet();
        var staleGroupBoards = await dbContext.Boards
            .Where(b => b.CurrentMatchId != null && deletedGroupIds.Contains(b.CurrentMatchId.Value))
            .ToListAsync(cancellationToken);
        foreach (var board in staleGroupBoards)
        {
            board.CurrentMatchId = null;
            board.CurrentMatchLabel = null;
            board.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        dbContext.Matches.RemoveRange(existingGroup);

        // Distribute participants into groups based on draw mode
        var groups = DistributeIntoGroups(participants, tournament.GroupCount, tournament.GroupDrawMode, tournament.SeedingEnabled, tournament.SeedTopCount);

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

        // In GroupAndKnockout mode, keep KO phase in sync with tournament plan creation.
        if (tournament.Mode == TournamentMode.GroupAndKnockout)
            await GenerateKnockoutPlanAsync(tournamentId, cancellationToken);

        // Auto-transition: plan created → Geplant
        await AutoAdvanceTournamentStatusAsync(tournamentId, cancellationToken);

        return await GetMatchesAsync(tournamentId, cancellationToken);
    }

    private static List<List<Participant>> DistributeIntoGroups(List<Participant> participants, int groupCount, GroupDrawMode drawMode, bool seedingEnabled, int seedTopCount)
    {
        var groups = Enumerable.Range(0, groupCount).Select(_ => new List<Participant>()).ToList();
        if (participants.Count == 0) return groups;

        static bool IsRanked(Participant p, bool enabled, int topCount)
            => enabled && topCount > 0 && p.Seed > 0 && p.Seed <= topCount;

        static int NextSmallestGroupIndex(List<List<Participant>> targetGroups)
            => targetGroups
                .Select((g, idx) => new { idx, count = g.Count, rand = Random.Shared.Next() })
                .OrderBy(x => x.count)
                .ThenBy(x => x.rand)
                .First().idx;

        if (drawMode == GroupDrawMode.SeededPots)
        {
            // Lostopf-Verfahren:
            // 1) Pots exist either from SeedPot field or are computed from ranking order.
            // 2) Each pot is shuffled.
            // 3) Draw is group-by-group: from each pot one random player per group.
            var hasAssignedPots = participants.Any(p => p.SeedPot > 0);
            List<List<Participant>> pots;

            if (hasAssignedPots)
            {
                pots = participants
                    .Where(p => p.SeedPot > 0)
                    .GroupBy(p => p.SeedPot)
                    .OrderBy(g => g.Key)
                    .Select(g => g.OrderBy(_ => Random.Shared.Next()).ToList())
                    .ToList();
            }
            else
            {
                var ranked = participants
                    .Where(p => IsRanked(p, seedingEnabled, seedTopCount))
                    .OrderBy(p => p.Seed)
                    .ToList();

                var unranked = participants
                    .Except(ranked)
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                var ordered = ranked.Concat(unranked).ToList();
                pots = [];
                for (var i = 0; i < ordered.Count; i += groupCount)
                {
                    pots.Add(ordered.Skip(i).Take(groupCount).OrderBy(_ => Random.Shared.Next()).ToList());
                }
            }

            foreach (var pot in pots)
            {
                var remaining = new List<Participant>(pot);
                for (var groupIndex = 0; groupIndex < groupCount && remaining.Count > 0; groupIndex++)
                {
                    var drawIndex = Random.Shared.Next(remaining.Count);
                    var picked = remaining[drawIndex];
                    remaining.RemoveAt(drawIndex);
                    groups[groupIndex].Add(picked);
                }
            }

            return groups;
        }

        // Zufällig ohne Lostopf:
        // - ohne Setzliste: vollständig zufällig, Gruppen annähernd gleich groß
        // - mit Setzliste: erst gerankte #1..#N zyklisch A..N, danach ungerankte zufällig auffüllen
        var rankedParticipants = participants
            .Where(p => IsRanked(p, seedingEnabled, seedTopCount))
            .OrderBy(p => p.Seed)
            .ToList();

        var remainingParticipants = participants
            .Except(rankedParticipants)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        if (rankedParticipants.Count > 0)
        {
            for (var i = 0; i < rankedParticipants.Count; i++)
            {
                groups[i % groupCount].Add(rankedParticipants[i]);
            }
        }

        foreach (var p in remainingParticipants)
        {
            groups[NextSmallestGroupIndex(groups)].Add(p);
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

        var participantsQuery = dbContext.Participants.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.GroupNumber != null);

        if (tournament.TeamplayEnabled)
            participantsQuery = participantsQuery.Where(x => x.Type == ParticipantType.TeamMember);

        var participants = await participantsQuery
            .ToListAsync(cancellationToken);

        if (participants.Count == 0)
            return [];

        var groupMatches = await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.Phase == MatchPhase.Group && x.FinishedUtc != null)
            .ToListAsync(cancellationToken);

        var groupMatchIds = groupMatches.Select(x => x.Id).ToList();
        var statisticsByParticipant = groupMatchIds.Count == 0
            ? new Dictionary<Guid, List<MatchPlayerStatistic>>()
            : (await dbContext.MatchPlayerStatistics.AsNoTracking()
                .Where(x => groupMatchIds.Contains(x.MatchId))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ParticipantId)
                .ToDictionary(g => g.Key, g => g.ToList());

        var enabledCriteria = await dbContext.ScoringCriteria.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.IsEnabled)
            .OrderBy(x => x.Priority)
            .Select(x => x.Type)
            .ToListAsync(cancellationToken);

        if (enabledCriteria.Count == 0)
        {
            enabledCriteria =
            [
                ScoringCriterionType.Points,
                ScoringCriterionType.DirectDuel
            ];
        }

        var standings = new List<GroupStandingDto>();

        foreach (var group in participants
            .GroupBy(x => x.GroupNumber ?? 0)
            .OrderBy(x => x.Key))
        {
            var groupParticipants = group.ToList();
            var groupNumber = group.Key;
            var groupMatchSet = groupMatches
                .Where(m => m.GroupNumber == groupNumber)
                .ToList();

            var rawStandings = groupParticipants
                .Select(p => BuildStandingAccumulator(p, groupMatchSet, statisticsByParticipant.GetValueOrDefault(p.Id), tournament))
                .ToList();

            var tiebreakerAppliedByParticipant = new Dictionary<Guid, string>();
            var ranked = RankByCriteriaRecursive(
                rawStandings,
                enabledCriteria,
                groupMatchSet,
                tournament,
                tiebreakerAppliedByParticipant,
                criterionIndex: 0);

            for (var i = 0; i < ranked.Count; i++)
            {
                var entry = ranked[i];
                standings.Add(new GroupStandingDto(
                    entry.ParticipantId,
                    entry.ParticipantName,
                    groupNumber,
                    entry.Played,
                    entry.Won,
                    entry.Lost,
                    entry.Points,
                    entry.LegsWon,
                    entry.LegsLost,
                    entry.LegDifference,
                    entry.Average,
                    entry.HighestAverage,
                    entry.HighestCheckout,
                    entry.AverageDartsPerLeg,
                    entry.CheckoutPercent,
                    entry.Breaks,
                    i + 1,
                    tiebreakerAppliedByParticipant.GetValueOrDefault(entry.ParticipantId)));
            }
        }

        return standings
            .OrderBy(s => s.GroupNumber)
            .ThenBy(s => s.Rank > 0 ? s.Rank : int.MaxValue)
            .ThenByDescending(s => s.Points)
            .ThenByDescending(s => s.LegDifference)
            .ToList();
    }

    private static StandingAccumulator BuildStandingAccumulator(
        Participant participant,
        IReadOnlyList<Match> groupMatches,
        IReadOnlyList<MatchPlayerStatistic>? statistics,
        Tournament tournament)
    {
        var playedMatches = groupMatches
            .Where(m => m.HomeParticipantId == participant.Id || m.AwayParticipantId == participant.Id)
            .ToList();

        var played = playedMatches.Count;
        var won = playedMatches.Count(m => m.WinnerParticipantId == participant.Id);
        var lost = played - won;

        var legsWon = playedMatches
            .Sum(m => m.HomeParticipantId == participant.Id ? m.HomeLegs : m.AwayLegs);
        var legsLost = playedMatches
            .Sum(m => m.HomeParticipantId == participant.Id ? m.AwayLegs : m.HomeLegs);

        var participantStats = statistics ?? [];
        var average = participantStats.Count > 0 ? participantStats.Average(x => x.Average) : 0d;
        var highestAverage = participantStats.Count > 0 ? participantStats.Max(x => x.Average) : 0d;
        var highestCheckout = participantStats.Count > 0 ? participantStats.Max(x => x.HighestCheckout) : 0;
        var averageDartsPerLeg = participantStats.Count > 0 ? participantStats.Average(x => x.AverageDartsPerLeg) : 0d;
        var checkoutAttempts = participantStats.Sum(x => x.CheckoutAttempts);
        var checkoutHits = participantStats.Sum(x => x.CheckoutHits);
        var checkoutPercent = checkoutAttempts > 0
            ? checkoutHits * 100d / checkoutAttempts
            : participantStats.Count > 0 ? participantStats.Average(x => x.CheckoutPercent) : 0d;
        var breaks = participantStats.Sum(x => x.Breaks);

        return new StandingAccumulator
        {
            ParticipantId = participant.Id,
            ParticipantName = participant.DisplayName,
            Played = played,
            Won = won,
            Lost = lost,
            Points = won * tournament.WinPoints + legsWon * tournament.LegFactor,
            LegsWon = legsWon,
            LegsLost = legsLost,
            LegDifference = legsWon - legsLost,
            Average = average,
            HighestAverage = highestAverage,
            HighestCheckout = highestCheckout,
            AverageDartsPerLeg = averageDartsPerLeg,
            CheckoutPercent = checkoutPercent,
            Breaks = breaks
        };
    }

    private static List<StandingAccumulator> RankByCriteriaRecursive(
        IReadOnlyList<StandingAccumulator> standings,
        IReadOnlyList<ScoringCriterionType> criteria,
        IReadOnlyList<Match> groupMatches,
        Tournament tournament,
        Dictionary<Guid, string> tiebreakerAppliedByParticipant,
        int criterionIndex)
    {
        if (standings.Count <= 1)
            return standings.OrderBy(x => x.ParticipantName, StringComparer.OrdinalIgnoreCase).ToList();

        if (criterionIndex >= criteria.Count)
            return standings.OrderBy(x => x.ParticipantName, StringComparer.OrdinalIgnoreCase).ToList();

        var criterion = criteria[criterionIndex];
        var criterionScores = BuildCriterionScores(standings, criterion, groupMatches, tournament);

        var ordered = standings
            .OrderByDescending(s => criterionScores.GetValueOrDefault(s.ParticipantId))
            .ThenBy(s => s.ParticipantName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scoreGroups = ordered
            .GroupBy(s => Math.Round(criterionScores.GetValueOrDefault(s.ParticipantId), 6))
            .ToList();

        if (scoreGroups.All(g => g.Count() == 1))
        {
            if (criterion != ScoringCriterionType.Points)
            {
                foreach (var participant in ordered)
                {
                    if (!tiebreakerAppliedByParticipant.ContainsKey(participant.ParticipantId))
                        tiebreakerAppliedByParticipant[participant.ParticipantId] = ScoringCriterionLabel(criterion);
                }
            }

            return ordered;
        }

        var result = new List<StandingAccumulator>(ordered.Count);

        foreach (var scoreGroup in scoreGroups)
        {
            var groupEntries = scoreGroup.ToList();
            if (groupEntries.Count == 1)
            {
                result.Add(groupEntries[0]);
                continue;
            }

            var rankedWithinTie = RankByCriteriaRecursive(
                groupEntries,
                criteria,
                groupMatches,
                tournament,
                tiebreakerAppliedByParticipant,
                criterionIndex + 1);

            if (criterion != ScoringCriterionType.Points)
            {
                foreach (var participant in rankedWithinTie)
                {
                    if (!tiebreakerAppliedByParticipant.ContainsKey(participant.ParticipantId))
                        tiebreakerAppliedByParticipant[participant.ParticipantId] = ScoringCriterionLabel(criterion);
                }
            }

            result.AddRange(rankedWithinTie);
        }

        return result;
    }

    private static Dictionary<Guid, double> BuildCriterionScores(
        IReadOnlyList<StandingAccumulator> standings,
        ScoringCriterionType criterion,
        IReadOnlyList<Match> groupMatches,
        Tournament tournament)
    {
        return criterion switch
        {
            ScoringCriterionType.Points => standings.ToDictionary(x => x.ParticipantId, x => (double)x.Points),
            ScoringCriterionType.DirectDuel => BuildDirectDuelScores(standings, groupMatches, tournament),
            ScoringCriterionType.WonLegs => standings.ToDictionary(x => x.ParticipantId, x => (double)x.LegsWon),
            ScoringCriterionType.LegDifference => standings.ToDictionary(x => x.ParticipantId, x => (double)x.LegDifference),
            ScoringCriterionType.Average => standings.ToDictionary(x => x.ParticipantId, x => x.Average),
            ScoringCriterionType.HighestAverage => standings.ToDictionary(x => x.ParticipantId, x => x.HighestAverage),
            ScoringCriterionType.Breaks => standings.ToDictionary(x => x.ParticipantId, x => (double)x.Breaks),
            ScoringCriterionType.HighestCheckout => standings.ToDictionary(x => x.ParticipantId, x => (double)x.HighestCheckout),
            ScoringCriterionType.AverageDartsPerLeg => standings.ToDictionary(x => x.ParticipantId, x => -x.AverageDartsPerLeg),
            ScoringCriterionType.CheckoutPercentage => standings.ToDictionary(x => x.ParticipantId, x => x.CheckoutPercent),
            ScoringCriterionType.LotDraw => standings.ToDictionary(x => x.ParticipantId, x => GetLotDrawScore(x.ParticipantId)),
            _ => standings.ToDictionary(x => x.ParticipantId, _ => 0d)
        };
    }

    private static Dictionary<Guid, double> BuildDirectDuelScores(
        IReadOnlyList<StandingAccumulator> standings,
        IReadOnlyList<Match> groupMatches,
        Tournament tournament)
    {
        var participantIds = standings.Select(x => x.ParticipantId).ToHashSet();
        var relevantMatches = groupMatches
            .Where(m => participantIds.Contains(m.HomeParticipantId) && participantIds.Contains(m.AwayParticipantId))
            .ToList();

        var scores = standings.ToDictionary(x => x.ParticipantId, _ => 0d);

        foreach (var match in relevantMatches)
        {
            var homeScore = (match.HomeLegs > match.AwayLegs ? tournament.WinPoints : 0)
                + match.HomeLegs * tournament.LegFactor;
            var awayScore = (match.AwayLegs > match.HomeLegs ? tournament.WinPoints : 0)
                + match.AwayLegs * tournament.LegFactor;

            if (scores.ContainsKey(match.HomeParticipantId))
                scores[match.HomeParticipantId] += homeScore;

            if (scores.ContainsKey(match.AwayParticipantId))
                scores[match.AwayParticipantId] += awayScore;
        }

        return scores;
    }

    private static double GetLotDrawScore(Guid participantId)
    {
        var bytes = participantId.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static string ScoringCriterionLabel(ScoringCriterionType criterion)
        => criterion switch
        {
            ScoringCriterionType.Points => "Punkte",
            ScoringCriterionType.DirectDuel => "Direktes Duell",
            ScoringCriterionType.WonLegs => "Gewonnene Legs",
            ScoringCriterionType.LegDifference => "Leg-Differenz",
            ScoringCriterionType.Average => "Average",
            ScoringCriterionType.HighestAverage => "Highest Average",
            ScoringCriterionType.Breaks => "Breaks",
            ScoringCriterionType.HighestCheckout => "Highest Checkout",
            ScoringCriterionType.AverageDartsPerLeg => "Average Darts/Leg",
            ScoringCriterionType.CheckoutPercentage => "Checkout-%",
            ScoringCriterionType.LotDraw => "Losentscheid",
            _ => criterion.ToString()
        };

    private sealed class StandingAccumulator
    {
        public Guid ParticipantId { get; init; }
        public required string ParticipantName { get; init; }
        public int Played { get; init; }
        public int Won { get; init; }
        public int Lost { get; init; }
        public int Points { get; init; }
        public int LegsWon { get; init; }
        public int LegsLost { get; init; }
        public int LegDifference { get; init; }
        public double Average { get; init; }
        public double HighestAverage { get; init; }
        public int HighestCheckout { get; init; }
        public double AverageDartsPerLeg { get; init; }
        public double CheckoutPercent { get; init; }
        public int Breaks { get; init; }
    }

    // ─── Result Reporting ───

    public async Task<MatchDto?> ReportResultAsync(ReportMatchResultRequest request, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == request.MatchId, cancellationToken);
        if (match is null) return null;

        match.ReportResult(request.HomeLegs, request.AwayLegs, request.HomeSets, request.AwaySets);
        match.RecomputeStatus();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Advance winner to next round in KO
        if (match.Phase == MatchPhase.Knockout && match.WinnerParticipantId.HasValue)
            await AdvanceWinnerAsync(match, cancellationToken);

        // Auto-transition tournament status (Gestartet / Beendet)
        await AutoAdvanceTournamentStatusAsync(match.TournamentId, cancellationToken);

        // Recalculate schedule to update SchedulingStatus and DelayMinutes
        await RecalculateScheduleAsync(match.TournamentId, cancellationToken);

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

        await EnsureBoardScopeAsync(match.TournamentId, boardId, cancellationToken);

        var previousBoardId = match.BoardId;
        match.BoardId = boardId;

        await EnsureBoardCurrentMatchConsistencyForChangedMatchAsync(match, previousBoardId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    public async Task<MatchDto?> SwapParticipantsAsync(Guid matchId, Guid participantId, Guid newParticipantId, CancellationToken cancellationToken = default)
    {
        var targetMatch = await dbContext.Matches.FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);
        if (targetMatch is null) return null;

        await EnsureTournamentStructureEditableAsync(targetMatch.TournamentId, cancellationToken);

        var allMatches = await dbContext.Matches.Where(x => x.TournamentId == targetMatch.TournamentId)
            .ToListAsync(cancellationToken);

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
            .ToListAsync(cancellationToken);

        var boards = await dbContext.Boards.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (boards.Count == 0)
        {
            boards = await dbContext.Boards.AsNoTracking()
                .Where(x => x.TournamentId == null)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        var startDateTime = tournament.StartDate.ToDateTime(tournament.StartTime.Value);
        var startUtc = ConvertLocalWallTimeToUtc(startDateTime, TimeZoneInfo.Local);

        if (boards.Count == 0)
            return await GetMatchesAsync(tournamentId, cancellationToken);

        // Simple sequential scheduling: assign times based on round settings
        var boardEndTimes = boards.ToDictionary(b => b.Id, _ => startUtc);
        var boardUsageCounts = boards.ToDictionary(b => b.Id, _ => 0);
        var boardIds = boards.Select(b => b.Id).ToList();
        var boardIndexById = boardIds
            .Select((boardId, index) => new { boardId, index })
            .ToDictionary(x => x.boardId, x => x.index);
        var nextRoundRobinBoardIndex = 0;
        var playerLastEnd = new Dictionary<Guid, DateTimeOffset>();

        foreach (var match in matches
            .Where(m => m.Status != MatchStatus.WalkOver)
            .OrderBy(m => GetSchedulePhaseBucket(m.Phase))
            .ThenBy(m => m.Phase == MatchPhase.Knockout ? m.Round : 0)
            .ThenBy(m => BuildScheduleSeed(m, startUtc))
            .ThenBy(m => m.GroupNumber ?? 0)
            .ThenBy(m => m.MatchNumber))
        {
            // Skip finished or started matches — don't replan them
            if (match.FinishedUtc is not null || match.StartedUtc is not null)
            {
                // Still track their board/player end times for subsequent scheduling
                var roundSettingsExisting = rounds.GetValueOrDefault((match.Phase, match.Round));
                var legSecsExisting = roundSettingsExisting?.LegDurationSeconds > 0 ? roundSettingsExisting.LegDurationSeconds : 300;
                var totalSecsExisting = (roundSettingsExisting?.Sets ?? 1) * (roundSettingsExisting?.Legs ?? 3) * legSecsExisting;
                var durationExisting = TimeSpan.FromSeconds(totalSecsExisting);
                var endExisting = (match.PlannedStartUtc?.ToUniversalTime() ?? startUtc) + durationExisting;
                if (match.BoardId.HasValue && boardEndTimes.ContainsKey(match.BoardId.Value))
                {
                    boardEndTimes[match.BoardId.Value] = endExisting > boardEndTimes[match.BoardId.Value] ? endExisting : boardEndTimes[match.BoardId.Value];
                    boardUsageCounts[match.BoardId.Value]++;
                }
                if (match.HomeParticipantId != Guid.Empty) playerLastEnd[match.HomeParticipantId] = endExisting;
                if (match.AwayParticipantId != Guid.Empty) playerLastEnd[match.AwayParticipantId] = endExisting;
                continue;
            }

            var roundSettings = rounds.GetValueOrDefault((match.Phase, match.Round));
            var legSecs = roundSettings?.LegDurationSeconds > 0 ? roundSettings.LegDurationSeconds : 300;
            var totalSecs = (roundSettings?.Sets ?? 1) * (roundSettings?.Legs ?? 3) * legSecs;
            var duration = TimeSpan.FromSeconds(totalSecs);
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
                var lockedStart = match.PlannedStartUtc.Value.ToUniversalTime();

                // Board assignment: respect locked boards
                if (!match.IsBoardLocked || match.BoardId is null)
                {
                    Guid? lockedBestBoard = null;
                    if (roundSettings?.BoardAssignment == BoardAssignmentMode.Fixed
                        && roundSettings.FixedBoardId.HasValue
                        && boardEndTimes.ContainsKey(roundSettings.FixedBoardId.Value))
                    {
                        lockedBestBoard = roundSettings.FixedBoardId.Value;
                    }
                    else
                    {
                        var freeBoardsAtLockedStart = boardIds
                            .Where(boardId => boardEndTimes[boardId] <= lockedStart)
                            .OrderBy(boardId => boardUsageCounts[boardId])
                            .ThenBy(boardId => GetRoundRobinDistance(boardIndexById[boardId], nextRoundRobinBoardIndex, boardIds.Count))
                            .ToList();

                        if (freeBoardsAtLockedStart.Count > 0)
                        {
                            lockedBestBoard = freeBoardsAtLockedStart[0];
                            nextRoundRobinBoardIndex = (boardIndexById[lockedBestBoard.Value] + 1) % boardIds.Count;
                        }

                        // If no board is free at locked time, pick earliest available
                        if (lockedBestBoard is null)
                        {
                            lockedBestBoard = SelectBestDynamicBoard(
                                boardIds,
                                boardIndexById,
                                boardEndTimes,
                                boardUsageCounts,
                                lockedStart,
                                ref nextRoundRobinBoardIndex);
                        }
                    }
                    if (lockedBestBoard.HasValue)
                    {
                        match.BoardId = lockedBestBoard;
                        boardUsageCounts[match.BoardId.Value]++;
                        boardEndTimes[match.BoardId!.Value] = lockedStart + duration + pause;
                    }
                }
                else if (match.BoardId.HasValue && boardEndTimes.ContainsKey(match.BoardId.Value))
                {
                    boardUsageCounts[match.BoardId.Value]++;
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
            else if (roundSettings?.BoardAssignment == BoardAssignmentMode.Fixed
                && roundSettings.FixedBoardId.HasValue
                && boardEndTimes.ContainsKey(roundSettings.FixedBoardId.Value))
            {
                bestBoard = roundSettings.FixedBoardId.Value;
                bestTime = boardEndTimes.GetValueOrDefault(bestBoard.Value, startUtc);
            }
            else
            {
                var hasExistingBoardHint = match.BoardId.HasValue
                    && match.PlannedStartUtc.HasValue
                    && boardEndTimes.ContainsKey(match.BoardId.Value);

                if (hasExistingBoardHint && match.BoardId is { } existingBoardId)
                {
                    bestBoard = existingBoardId;
                    bestTime = boardEndTimes.GetValueOrDefault(existingBoardId, startUtc);
                }
                else
                {
                    bestBoard = SelectBestDynamicBoard(
                        boardIds,
                        boardIndexById,
                        boardEndTimes,
                        boardUsageCounts,
                        earliest,
                        ref nextRoundRobinBoardIndex);
                    bestTime = bestBoard.HasValue
                        ? boardEndTimes.GetValueOrDefault(bestBoard.Value, startUtc)
                        : startUtc;
                }
            }

            var matchStart = bestTime > earliest ? bestTime : earliest;
            match.PlannedStartUtc = matchStart.ToUniversalTime();
            if (bestBoard.HasValue)
            {
                match.BoardId = bestBoard;
                boardUsageCounts[bestBoard.Value]++;
                boardEndTimes[bestBoard.Value] = matchStart + duration + pause;
            }

            var matchEnd = matchStart + duration;
            if (match.HomeParticipantId != Guid.Empty) playerLastEnd[match.HomeParticipantId] = matchEnd;
            if (match.AwayParticipantId != Guid.Empty) playerLastEnd[match.AwayParticipantId] = matchEnd;
        }

        // Recompute match statuses so that matches with PlannedStartUtc transition from Erstellt to Geplant
        foreach (var match in matches.Where(m => m.Status != MatchStatus.WalkOver))
        {
            match.RecomputeStatus();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetMatchesAsync(tournamentId, cancellationToken);
    }

    private static Guid? SelectBestDynamicBoard(
        IReadOnlyList<Guid> boardIds,
        IReadOnlyDictionary<Guid, int> boardIndexById,
        IReadOnlyDictionary<Guid, DateTimeOffset> boardEndTimes,
        IReadOnlyDictionary<Guid, int> boardUsageCounts,
        DateTimeOffset earliestStart,
        ref int nextRoundRobinBoardIndex)
    {
        if (boardIds.Count == 0)
            return null;

        Guid? bestBoardId = null;
        DateTimeOffset bestBoardStart = default;
        var bestUsage = int.MaxValue;
        var bestDistance = int.MaxValue;

        foreach (var boardId in boardIds)
        {
            var boardAvailableAt = boardEndTimes.GetValueOrDefault(boardId, earliestStart);
            var boardStart = boardAvailableAt > earliestStart ? boardAvailableAt : earliestStart;
            var usage = boardUsageCounts.GetValueOrDefault(boardId);
            var distance = GetRoundRobinDistance(boardIndexById[boardId], nextRoundRobinBoardIndex, boardIds.Count);

            if (bestBoardId is null
                || boardStart < bestBoardStart
                || (boardStart == bestBoardStart && usage < bestUsage)
                || (boardStart == bestBoardStart && usage == bestUsage && distance < bestDistance))
            {
                bestBoardId = boardId;
                bestBoardStart = boardStart;
                bestUsage = usage;
                bestDistance = distance;
            }
        }

        if (bestBoardId.HasValue)
            nextRoundRobinBoardIndex = (boardIndexById[bestBoardId.Value] + 1) % boardIds.Count;

        return bestBoardId;
    }

    private static int GetRoundRobinDistance(int boardIndex, int nextBoardIndex, int boardCount)
        => (boardIndex - nextBoardIndex + boardCount) % boardCount;

    private static int GetSchedulePhaseBucket(MatchPhase phase)
        => phase == MatchPhase.Group ? 0 : 1;

    private static DateTimeOffset BuildScheduleSeed(Match match, DateTimeOffset scheduleStartUtc)
    {
        if (match.PlannedStartUtc.HasValue)
            return match.PlannedStartUtc.Value.ToUniversalTime();

        var phaseOffset = GetSchedulePhaseBucket(match.Phase) * 365 * 24 * 60;
        var roundOffset = match.Phase == MatchPhase.Group ? 0 : match.Round * 60;
        var groupOffset = (match.GroupNumber ?? 0) * 10;
        return scheduleStartUtc
            .AddMinutes(phaseOffset)
            .AddMinutes(roundOffset)
            .AddMinutes(groupOffset)
            .AddSeconds(match.MatchNumber);
    }

    // ─── Prediction ───

    public async Task<MatchDto?> UpdateMatchScheduleAsync(Guid matchId, DateTimeOffset? startTime, bool lockTime, Guid? boardId, bool lockBoard, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
        if (match is null) return null;

        if (boardId.HasValue)
            await EnsureBoardScopeAsync(match.TournamentId, boardId.Value, cancellationToken);

        var previousBoardId = match.BoardId;

        match.PlannedStartUtc = startTime?.ToUniversalTime();
        match.IsStartTimeLocked = lockTime;
        if (boardId.HasValue) match.BoardId = boardId;
        match.IsBoardLocked = lockBoard;

        await EnsureBoardCurrentMatchConsistencyForChangedMatchAsync(match, previousBoardId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    private async Task EnsureBoardScopeAsync(Guid tournamentId, Guid boardId, CancellationToken cancellationToken)
    {
        var boardScope = await dbContext.Boards
            .AsNoTracking()
            .Where(x => x.Id == boardId)
            .Select(x => new { x.TournamentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (boardScope is null)
            throw new InvalidOperationException("Board nicht gefunden.");

        if (boardScope.TournamentId.HasValue && boardScope.TournamentId.Value != tournamentId)
            throw new InvalidOperationException("Board gehoert zu einem anderen Turnier.");
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
            match.RecomputeStatus();
            await dbContext.SaveChangesAsync(cancellationToken);

            if (match.Phase == MatchPhase.Knockout && match.WinnerParticipantId.HasValue)
                await AdvanceWinnerAsync(match, cancellationToken);
        }
        else
        {
            match.RecomputeStatus();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Auto-transition tournament status (Gestartet / Beendet)
        await AutoAdvanceTournamentStatusAsync(match.TournamentId, cancellationToken);

        // Recalculate schedule to update SchedulingStatus and DelayMinutes
        await RecalculateScheduleAsync(match.TournamentId, cancellationToken);

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
        match.RecomputeStatus();

        await CleanupBoardCurrentMatchPointersAsync(new HashSet<Guid> { match.Id }, cancellationToken);

        var statsToDelete = await dbContext.MatchPlayerStatistics
            .Where(x => x.MatchId == matchId)
            .ToListAsync(cancellationToken);
        dbContext.MatchPlayerStatistics.RemoveRange(statsToDelete);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    // ─── Auto-Transition Tournament Status ───

    private async Task AutoAdvanceTournamentStatusAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null || tournament.Status == TournamentStatus.Abgebrochen) return;

        var allMatches = await dbContext.Matches.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var realMatches = allMatches.Where(m => m.Status != MatchStatus.WalkOver
            && m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty).ToList();

        // Erstellt → Geplant: matches exist (plan was generated)
        if (tournament.Status == TournamentStatus.Erstellt && allMatches.Count > 0)
        {
            tournament.Status = TournamentStatus.Geplant;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Geplant → Gestartet: any real match is active (has StartedUtc or ExternalMatchId)
        if (tournament.Status == TournamentStatus.Geplant && realMatches.Any(m => m.StartedUtc is not null || m.ExternalMatchId is not null))
        {
            tournament.Status = TournamentStatus.Gestartet;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Gestartet → Beendet: finale is finished
        if (tournament.Status == TournamentStatus.Gestartet)
        {
            var koMatches = allMatches.Where(m => m.Phase == MatchPhase.Knockout).ToList();
            if (koMatches.Count > 0)
            {
                var maxRound = koMatches.Where(m => m.Status != MatchStatus.WalkOver).Select(m => m.Round).DefaultIfEmpty(0).Max();
                var finale = koMatches.Where(m => m.Round == maxRound && m.Status != MatchStatus.WalkOver).ToList();
                if (finale.Count > 0 && finale.All(m => m.FinishedUtc is not null))
                {
                    tournament.Status = TournamentStatus.Beendet;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }
            }
            else if (realMatches.Count > 0 && realMatches.All(m => m.FinishedUtc is not null))
            {
                // No KO phase — all matches finished (e.g., group-only tournament)
                tournament.Status = TournamentStatus.Beendet;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
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

    public async Task<MatchDto?> UpdateMatchAsync(UpdateMatchRequest request, CancellationToken cancellationToken = default)
    {
        var match = await dbContext.Matches.FirstOrDefaultAsync(x => x.Id == request.MatchId, cancellationToken);
        if (match is null) return null;

        var previousBoardId = match.BoardId;

        match.BoardId = request.BoardId;
        match.HomeLegs = request.HomeLegs;
        match.AwayLegs = request.AwayLegs;
        match.HomeSets = request.HomeSets;
        match.AwaySets = request.AwaySets;
        match.IsStartTimeLocked = request.IsStartTimeLocked;
        match.IsBoardLocked = request.IsBoardLocked;
        match.WinnerParticipantId = request.WinnerParticipantId;

        if (Enum.TryParse<MatchStatus>(request.Status, out var status))
            match.Status = status;

        await EnsureBoardCurrentMatchConsistencyForChangedMatchAsync(match, previousBoardId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(match);
    }

    public async Task<IReadOnlyList<MatchDto>> BatchResetMatchesAsync(IReadOnlyList<Guid> matchIds, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.Matches
            .Where(x => matchIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var match in matches)
        {
            match.HomeLegs = 0;
            match.AwayLegs = 0;
            match.HomeSets = 0;
            match.AwaySets = 0;
            match.WinnerParticipantId = null;
            match.StartedUtc = null;
            match.FinishedUtc = null;
            match.ExternalMatchId = null;
            match.RecomputeStatus();
        }

        var resetIds = matches.Select(m => m.Id).ToHashSet();
        await CleanupBoardCurrentMatchPointersAsync(resetIds, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return matches.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<MatchDto>> CleanupStaleMatchesAsync(Guid tournamentId, int staleMinutes, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-staleMinutes);
        var allMatches = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        // Sonderfall: Turnier im Status 'Erstellt' und keine Gruppenmatches -> alle KO-Matches löschen
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        bool isDraft = tournament != null && tournament.Status == TournamentStatus.Erstellt;
        bool hasNoGroupMatches = !allMatches.Any(x => x.Phase == MatchPhase.Group);
        var koMatches = allMatches.Where(x => x.Phase == MatchPhase.Knockout).ToList();
        if (isDraft && hasNoGroupMatches && koMatches.Count > 0)
        {
            var deletedKoIds = koMatches.Select(x => x.Id).ToHashSet();
            dbContext.Matches.RemoveRange(koMatches);
            await dbContext.SaveChangesAsync(cancellationToken);

            await CleanupBoardCurrentMatchPointersAsync(deletedKoIds, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return koMatches.Select(ToDto).ToList();
        }

        var staleMatches = allMatches
            .Where(x => x.StartedUtc != null
                && x.StartedUtc < cutoff
                && x.FinishedUtc == null
                && x.Status != MatchStatus.WalkOver
                && x.Status != MatchStatus.Beendet)
            .ToList();

        // Also include bye matches (exactly one participant is empty) that don't have correct WalkOver status
        var byeMatches = allMatches
            .Where(x => (x.HomeParticipantId == Guid.Empty) != (x.AwayParticipantId == Guid.Empty))
            .Where(x => x.Status != MatchStatus.WalkOver)
            .ToList();

        var matchesToClean = staleMatches.Union(byeMatches).Distinct().ToList();
        var cleanedMatchIds = matchesToClean.Select(m => m.Id).ToHashSet();

        foreach (var match in matchesToClean)
        {
            match.HomeLegs = 0;
            match.AwayLegs = 0;
            match.HomeSets = 0;
            match.AwaySets = 0;
            match.WinnerParticipantId = null;
            match.StartedUtc = null;
            match.FinishedUtc = null;
            match.ExternalMatchId = null;
            match.RecomputeStatus();
        }

        await CleanupBoardCurrentMatchPointersAsync(cleanedMatchIds, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return matchesToClean.Select(ToDto).ToList();
    }

    private static bool IsCurrentlyActiveMatch(Match match)
        => match.StartedUtc is not null && match.FinishedUtc is null;

    private static void ClearBoardCurrentMatch(Board board, DateTimeOffset now)
    {
        board.CurrentMatchId = null;
        board.CurrentMatchLabel = null;
        board.UpdatedUtc = now;
    }

    private async Task<string> BuildMatchLabelAsync(Match match, CancellationToken cancellationToken)
    {
        var participantIds = new[] { match.HomeParticipantId, match.AwayParticipantId }
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var participants = await dbContext.Participants
            .Where(x => participantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.AccountName : x.DisplayName, cancellationToken);

        static string ResolveName(Guid participantId, IReadOnlyDictionary<Guid, string> names)
            => participantId == Guid.Empty ? "BYE" : names.TryGetValue(participantId, out var name) ? name : "?";

        var home = ResolveName(match.HomeParticipantId, participants);
        var away = ResolveName(match.AwayParticipantId, participants);
        return $"{home} vs {away}";
    }

    private async Task EnsureBoardCurrentMatchConsistencyForChangedMatchAsync(Match match, Guid? previousBoardId, CancellationToken cancellationToken)
    {
        var relevantBoardIds = new HashSet<Guid>();
        if (previousBoardId.HasValue)
            relevantBoardIds.Add(previousBoardId.Value);
        if (match.BoardId.HasValue)
            relevantBoardIds.Add(match.BoardId.Value);

        var boards = await dbContext.Boards
            .Where(b => relevantBoardIds.Contains(b.Id) || b.CurrentMatchId == match.Id)
            .ToListAsync(cancellationToken);

        if (boards.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;

        foreach (var board in boards.Where(b => b.CurrentMatchId == match.Id))
        {
            if (match.BoardId != board.Id || !IsCurrentlyActiveMatch(match))
                ClearBoardCurrentMatch(board, now);
        }

        if (match.BoardId.HasValue && IsCurrentlyActiveMatch(match))
        {
            var targetBoard = boards.FirstOrDefault(b => b.Id == match.BoardId.Value);
            if (targetBoard is not null)
            {
                targetBoard.CurrentMatchId = match.Id;
                targetBoard.CurrentMatchLabel = await BuildMatchLabelAsync(match, cancellationToken);
                targetBoard.UpdatedUtc = now;
            }
        }
    }

    private async Task CleanupBoardCurrentMatchPointersAsync(ISet<Guid> cleanedMatchIds, CancellationToken cancellationToken)
    {
        var boardsWithCurrentMatch = await dbContext.Boards
            .Where(b => b.CurrentMatchId != null)
            .ToListAsync(cancellationToken);

        if (boardsWithCurrentMatch.Count == 0)
            return;

        var referencedMatchIds = boardsWithCurrentMatch
            .Select(b => b.CurrentMatchId!.Value)
            .Distinct()
            .ToList();

        var referencedMatches = await dbContext.Matches
            .Where(m => referencedMatchIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var board in boardsWithCurrentMatch)
        {
            var currentMatchId = board.CurrentMatchId!.Value;

            if (!referencedMatches.TryGetValue(currentMatchId, out var match)
                || cleanedMatchIds.Contains(currentMatchId)
                || match.BoardId != board.Id
                || !IsCurrentlyActiveMatch(match))
            {
                ClearBoardCurrentMatch(board, now);
            }
        }
    }

    // Statistics (#18)
    public async Task<IReadOnlyList<MatchPlayerStatisticDto>> GetMatchStatisticsAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var stats = await dbContext.MatchPlayerStatistics.AsNoTracking()
            .Where(x => x.MatchId == matchId)
            .ToListAsync(cancellationToken);

        var participantIds = stats.Select(s => s.ParticipantId).Distinct().ToList();
        var participants = await dbContext.Participants.AsNoTracking()
            .Where(p => participantIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        return stats.Select(s => ToStatDto(s, participants.GetValueOrDefault(s.ParticipantId, ""))).ToList();
    }

    public async Task<MatchPlayerStatisticDto> SaveMatchPlayerStatisticAsync(MatchPlayerStatisticDto statistic, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MatchPlayerStatistics
            .FirstOrDefaultAsync(x => x.MatchId == statistic.MatchId && x.ParticipantId == statistic.ParticipantId, cancellationToken);

        if (existing is null)
        {
            existing = new MatchPlayerStatistic
            {
                MatchId = statistic.MatchId,
                ParticipantId = statistic.ParticipantId
            };
            dbContext.MatchPlayerStatistics.Add(existing);
        }

        existing.Average = statistic.Average;
        existing.First9Average = statistic.First9Average;
        existing.DartsThrown = statistic.DartsThrown;
        existing.LegsWon = statistic.LegsWon;
        existing.LegsLost = statistic.LegsLost;
        existing.SetsWon = statistic.SetsWon;
        existing.SetsLost = statistic.SetsLost;
        existing.HighestCheckout = statistic.HighestCheckout;
        existing.CheckoutPercent = statistic.CheckoutPercent;
        existing.CheckoutHits = statistic.CheckoutHits;
        existing.CheckoutAttempts = statistic.CheckoutAttempts;
        existing.Plus100 = statistic.Plus100;
        existing.Plus140 = statistic.Plus140;
        existing.Plus170 = statistic.Plus170;
        existing.Plus180 = statistic.Plus180;
        existing.Breaks = statistic.Breaks;
        existing.AverageDartsPerLeg = statistic.AverageDartsPerLeg;
        existing.BestLegDarts = statistic.BestLegDarts;
        existing.WorstLegDarts = statistic.WorstLegDarts;
        existing.TonPlusCheckouts = statistic.TonPlusCheckouts;
        existing.DoubleQuota = statistic.DoubleQuota;
        existing.TotalPoints = statistic.TotalPoints;
        existing.HighestRoundScore = statistic.HighestRoundScore;

        await dbContext.SaveChangesAsync(cancellationToken);

        var participant = await dbContext.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == existing.ParticipantId, cancellationToken);
        return ToStatDto(existing, participant?.DisplayName ?? "");
    }

    public async Task<IReadOnlyList<MatchPlayerStatisticDto>> SyncStatisticsFromExternalAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        // Placeholder: in a real implementation, this would call the autodarts API
        // via IAutodartsClient to fetch live stats. For now, return existing stats.
        return await GetMatchStatisticsAsync(matchId, cancellationToken);
    }

    // Followers (#14)
    public async Task<IReadOnlyList<MatchFollowerDto>> GetMatchFollowersAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        return await dbContext.MatchFollowers.AsNoTracking()
            .Where(x => x.MatchId == matchId)
            .Select(x => new MatchFollowerDto(x.Id, x.MatchId, x.UserAccountName, x.CreatedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<MatchFollowerDto> FollowMatchAsync(Guid matchId, string userAccountName, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MatchFollowers
            .FirstOrDefaultAsync(x => x.MatchId == matchId && x.UserAccountName == userAccountName, cancellationToken);
        if (existing is not null)
            return new MatchFollowerDto(existing.Id, existing.MatchId, existing.UserAccountName, existing.CreatedUtc);

        var follower = new MatchFollower
        {
            MatchId = matchId,
            UserAccountName = userAccountName,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.MatchFollowers.Add(follower);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MatchFollowerDto(follower.Id, follower.MatchId, follower.UserAccountName, follower.CreatedUtc);
    }

    public async Task<bool> UnfollowMatchAsync(Guid matchId, string userAccountName, CancellationToken cancellationToken = default)
    {
        var follower = await dbContext.MatchFollowers
            .FirstOrDefaultAsync(x => x.MatchId == matchId && x.UserAccountName == userAccountName, cancellationToken);
        if (follower is null) return false;

        dbContext.MatchFollowers.Remove(follower);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Scheduling (#12)
    public async Task<IReadOnlyList<MatchDto>> RecalculateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.Matches
            .Where(x => x.TournamentId == tournamentId && x.FinishedUtc == null)
            .OrderBy(x => x.PlannedStartUtc)
            .ThenBy(x => x.Round)
            .ThenBy(x => x.MatchNumber)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var match in matches)
        {
            if (match.PlannedStartUtc.HasValue && match.PlannedEndUtc.HasValue)
            {
                if (match.StartedUtc.HasValue && match.FinishedUtc == null)
                {
                    // Running match - estimate end time based on current progress
                    var elapsed = now - match.StartedUtc.Value;
                    var planned = match.PlannedEndUtc.Value - match.PlannedStartUtc.Value;
                    if (elapsed > planned)
                    {
                        match.DelayMinutes = (int)(elapsed - planned).TotalMinutes;
                        match.SchedulingStatus = SchedulingStatus.Delayed;
                    }
                    else
                    {
                        match.DelayMinutes = 0;
                        match.SchedulingStatus = elapsed < planned * 0.8 ? SchedulingStatus.Ahead : SchedulingStatus.InTime;
                    }
                    match.ExpectedEndUtc = now + (planned - elapsed);
                }
                else if (match.StartedUtc == null)
                {
                    // Planned match that hasn't started yet
                    // Check if it's delayed (planned start time has passed)
                    if (now > match.PlannedStartUtc.Value)
                    {
                        match.DelayMinutes = (int)(now - match.PlannedStartUtc.Value).TotalMinutes;
                        match.SchedulingStatus = SchedulingStatus.Delayed;
                    }
                    else
                    {
                        match.DelayMinutes = 0;
                        match.SchedulingStatus = SchedulingStatus.None;
                    }
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

    private static MatchPlayerStatisticDto ToStatDto(MatchPlayerStatistic s, string participantName) => new(
        s.Id, s.MatchId, s.ParticipantId, participantName,
        s.Average, s.First9Average, s.DartsThrown, s.LegsWon, s.LegsLost,
        s.SetsWon, s.SetsLost, s.HighestCheckout, s.CheckoutPercent,
        s.CheckoutHits, s.CheckoutAttempts, s.Plus100, s.Plus140, s.Plus170, s.Plus180,
        s.Breaks, s.AverageDartsPerLeg, s.BestLegDarts, s.WorstLegDarts,
        s.TonPlusCheckouts, s.DoubleQuota, s.TotalPoints, s.HighestRoundScore);

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