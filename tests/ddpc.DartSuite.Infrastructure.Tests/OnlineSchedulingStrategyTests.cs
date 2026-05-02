using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using ddpc.DartSuite.Domain.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class OnlineSchedulingStrategyTests
{
    private static readonly DateTimeOffset TournamentStart =
        new(2025, 6, 1, 18, 0, 0, TimeSpan.Zero);

    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Tournament BuildOnlineTournament(Guid id) => new()
    {
        Id = id,
        Name = "Online Cup",
        OrganizerAccount = "manager",
        StartDate = DateOnly.FromDateTime(TournamentStart.Date),
        EndDate = DateOnly.FromDateTime(TournamentStart.Date),
        StartTime = TimeOnly.FromTimeSpan(TournamentStart.TimeOfDay),
        Variant = TournamentVariant.Online
    };

    // ── Helper to build a default round def (15 min duration: 1 set × 3 legs × 300s) ──
    private static TournamentRound DefaultRound(Guid tournamentId, int roundNumber = 1, MatchPhase phase = MatchPhase.Knockout) => new()
    {
        TournamentId = tournamentId,
        RoundNumber = roundNumber,
        Phase = phase,
        Legs = 3,
        Sets = 1,
        LegDurationSeconds = 300 // 5 min/leg → 15 min total
    };

    // ── Test 1: Player A's match ends at 18:20, next match of A starts ≥ 18:20 ──
    [Fact]
    public async Task PlayerAvailability_NextMatchStartsAfterPreviousEnd()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(BuildOnlineTournament(tournamentId));

        db.TournamentRounds.Add(DefaultRound(tournamentId));

        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var playerC = Guid.NewGuid();

        // Match 1: A vs B starts at 18:00, duration 15 min → ends 18:15
        // Match 2: A vs C — A is available from 18:15
        var match1 = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = playerA,
            AwayParticipantId = playerB
        };
        var match2 = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Knockout,
            Round = 1,
            MatchNumber = 2,
            HomeParticipantId = playerA,
            AwayParticipantId = playerC
        };

        db.Matches.AddRange(match1, match2);
        await db.SaveChangesAsync();

        var result = await service.GenerateScheduleAsync(tournamentId);

        var r1 = result.Single(m => m.Id == match1.Id);
        var r2 = result.Single(m => m.Id == match2.Id);

        r1.PlannedStartUtc.Should().NotBeNull();
        r2.PlannedStartUtc.Should().NotBeNull();
        // Match 2 (player A's next match) must start at or after match 1 ends
        var match1End = r1.PlannedStartUtc!.Value.AddMinutes(15);
        r2.PlannedStartUtc!.Value.Should().BeOnOrAfter(match1End);
    }

    // ── Test 2: start(M) = max(availA, availB, RoundDateTime) ──
    [Fact]
    public async Task MatchStart_IsMaxOfBothPlayersAvailAndRoundDateTime()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(BuildOnlineTournament(tournamentId));
        db.TournamentRounds.Add(DefaultRound(tournamentId));

        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var playerC = Guid.NewGuid();
        var playerD = Guid.NewGuid();

        // A ends at T+15min, B ends at T+30min → match (A vs B) should start at T+30min
        var m1 = new Match // A vs C, starts at T → A busy until T+15
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 1,
            HomeParticipantId = playerA, AwayParticipantId = playerC
        };
        var m2 = new Match // B vs D, starts at T+15 → B busy until T+30
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 2,
            HomeParticipantId = playerB, AwayParticipantId = playerD,
            PlannedStartUtc = TournamentStart.AddMinutes(15),
            IsStartTimeLocked = true
        };
        var mTarget = new Match // A vs B → should start at T+30
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 3,
            HomeParticipantId = playerA, AwayParticipantId = playerB
        };

        db.Matches.AddRange(m1, m2, mTarget);
        await db.SaveChangesAsync();

        var result = await service.GenerateScheduleAsync(tournamentId);

        var r = result.Single(m => m.Id == mTarget.Id);
        r.PlannedStartUtc.Should().NotBeNull();
        r.PlannedStartUtc!.Value.Should().BeOnOrAfter(TournamentStart.AddMinutes(30));
    }

    // ── Test 3: RoundDate in future blocks scheduling before that date ──
    [Fact]
    public async Task RoundDate_BlocksSchedulingBeforeThatDate()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(BuildOnlineTournament(tournamentId));

        var futureDate = DateOnly.FromDateTime(TournamentStart.Date.AddDays(7));
        db.TournamentRounds.Add(new TournamentRound
        {
            TournamentId = tournamentId,
            RoundNumber = 1,
            Phase = MatchPhase.Knockout,
            Legs = 3,
            Sets = 1,
            LegDurationSeconds = 300,
            RoundDate = futureDate,
            RoundStartTime = new TimeOnly(20, 0)
        });

        var match = new Match
        {
            TournamentId = tournamentId,
            Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid()
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var result = await service.GenerateScheduleAsync(tournamentId);
        var r = result.Single(m => m.Id == match.Id);

        r.PlannedStartUtc.Should().NotBeNull();
        // Must not be scheduled before the round date
        var roundDateStart = new DateTimeOffset(
            futureDate.ToDateTime(new TimeOnly(20, 0)),
            TimeZoneInfo.Local.GetUtcOffset(futureDate.ToDateTime(new TimeOnly(20, 0))));
        r.PlannedStartUtc!.Value.ToUniversalTime()
            .Should().BeOnOrAfter(roundDateStart.ToUniversalTime());
    }

    // ── Test 4: Two locked matches on same board must not overlap ──
    [Fact]
    public async Task LockedBoardMatches_DoNotOverlap()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(BuildOnlineTournament(tournamentId));
        db.TournamentRounds.Add(DefaultRound(tournamentId));

        var boardId = Guid.NewGuid();

        // Both matches locked to same board at T+0 — second must be pushed to T+15
        var m1 = new Match
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(), AwayParticipantId = Guid.NewGuid(),
            BoardId = boardId,
            PlannedStartUtc = TournamentStart,
            IsStartTimeLocked = true,
            IsBoardLocked = true
        };
        var m2 = new Match
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 2,
            HomeParticipantId = Guid.NewGuid(), AwayParticipantId = Guid.NewGuid(),
            BoardId = boardId,
            IsBoardLocked = true  // board locked but start time not locked → scheduler finds non-overlapping slot
        };

        db.Matches.AddRange(m1, m2);
        await db.SaveChangesAsync();

        var result = await service.GenerateScheduleAsync(tournamentId);

        var r1 = result.Single(m => m.Id == m1.Id);
        var r2 = result.Single(m => m.Id == m2.Id);

        r1.PlannedStartUtc.Should().NotBeNull();
        r2.PlannedStartUtc.Should().NotBeNull();

        var start1 = r1.PlannedStartUtc!.Value;
        var end1 = start1.AddMinutes(15);
        var start2 = r2.PlannedStartUtc!.Value;
        var end2 = start2.AddMinutes(15);

        // Board-locked matches must not overlap in time; ordering may differ.
        var hasOverlap = start1 < end2 && start2 < end1;
        hasOverlap.Should().BeFalse();
    }

    // ── Test 5: Online tournament has no board assignment for unlocked matches ──
    [Fact]
    public async Task OnlineScheduling_DoesNotAssignBoards_ToUnlockedMatches()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(BuildOnlineTournament(tournamentId));
        db.TournamentRounds.Add(DefaultRound(tournamentId));

        // Add tournament boards — online scheduling should NOT assign them
        db.Boards.Add(new Board { Id = Guid.NewGuid(), ExternalBoardId = "b1", Name = "Board 1", TournamentId = tournamentId });

        var match = new Match
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(), AwayParticipantId = Guid.NewGuid()
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var result = await service.GenerateScheduleAsync(tournamentId);
        var r = result.Single(m => m.Id == match.Id);

        r.PlannedStartUtc.Should().NotBeNull("Online scheduling still assigns start times");
        r.BoardId.Should().BeNull("Online scheduling does not assign boards to unlocked matches");
    }

    // ── Test 6: OnSite tournament (existing behaviour) still uses boards ──
    [Fact]
    public async Task OnSiteScheduling_StillAssignsBoards()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(new Tournament
        {
            Id = tournamentId,
            Name = "OnSite Cup",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(TournamentStart.Date),
            EndDate = DateOnly.FromDateTime(TournamentStart.Date),
            StartTime = TimeOnly.FromTimeSpan(TournamentStart.TimeOfDay),
            Variant = TournamentVariant.OnSite
        });

        db.Boards.Add(new Board { Id = Guid.NewGuid(), ExternalBoardId = "b1", Name = "Board 1", TournamentId = tournamentId });

        db.Matches.Add(new Match
        {
            TournamentId = tournamentId, Phase = MatchPhase.Knockout,
            Round = 1, MatchNumber = 1,
            HomeParticipantId = Guid.NewGuid(), AwayParticipantId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var result = await service.GenerateScheduleAsync(tournamentId);
        result.Should().ContainSingle(m => m.BoardId.HasValue, "OnSite scheduling must still assign boards");
    }
}
