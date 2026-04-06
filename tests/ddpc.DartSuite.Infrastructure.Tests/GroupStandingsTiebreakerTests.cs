using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class GroupStandingsTiebreakerTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetGroupStandings_ShouldApplyConfiguredCriteriaOrder_AndExposeAppliedTiebreaker()
    {
        await using var db = CreateDbContext();
        var service = new MatchManagementService(db, new MatchPredictionService());

        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(new Tournament
        {
            Id = tournamentId,
            Name = "Tiebreak Cup",
            OrganizerAccount = "manager",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today),
            Mode = TournamentMode.GroupAndKnockout,
            GroupCount = 1,
            WinPoints = 2,
            LegFactor = 0
        });

        var participantA = new Participant
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            DisplayName = "Alpha",
            AccountName = "alpha",
            GroupNumber = 1
        };
        var participantB = new Participant
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            DisplayName = "Bravo",
            AccountName = "bravo",
            GroupNumber = 1
        };
        var participantC = new Participant
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            DisplayName = "Charlie",
            AccountName = "charlie",
            GroupNumber = 1
        };
        db.Participants.AddRange(participantA, participantB, participantC);

        var match1 = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            GroupNumber = 1,
            Round = 1,
            MatchNumber = 1,
            HomeParticipantId = participantA.Id,
            AwayParticipantId = participantB.Id,
            HomeLegs = 3,
            AwayLegs = 2,
            WinnerParticipantId = participantA.Id,
            FinishedUtc = DateTimeOffset.UtcNow
        };
        var match2 = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            GroupNumber = 1,
            Round = 1,
            MatchNumber = 2,
            HomeParticipantId = participantB.Id,
            AwayParticipantId = participantC.Id,
            HomeLegs = 3,
            AwayLegs = 2,
            WinnerParticipantId = participantB.Id,
            FinishedUtc = DateTimeOffset.UtcNow
        };
        var match3 = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Phase = MatchPhase.Group,
            GroupNumber = 1,
            Round = 1,
            MatchNumber = 3,
            HomeParticipantId = participantC.Id,
            AwayParticipantId = participantA.Id,
            HomeLegs = 3,
            AwayLegs = 2,
            WinnerParticipantId = participantC.Id,
            FinishedUtc = DateTimeOffset.UtcNow
        };
        db.Matches.AddRange(match1, match2, match3);

        db.MatchPlayerStatistics.AddRange(
            new MatchPlayerStatistic { MatchId = match1.Id, ParticipantId = participantA.Id, Average = 90, AverageDartsPerLeg = 18, CheckoutPercent = 35 },
            new MatchPlayerStatistic { MatchId = match1.Id, ParticipantId = participantB.Id, Average = 80, AverageDartsPerLeg = 20, CheckoutPercent = 30 },
            new MatchPlayerStatistic { MatchId = match2.Id, ParticipantId = participantB.Id, Average = 95, AverageDartsPerLeg = 17, CheckoutPercent = 45 },
            new MatchPlayerStatistic { MatchId = match2.Id, ParticipantId = participantC.Id, Average = 75, AverageDartsPerLeg = 21, CheckoutPercent = 28 },
            new MatchPlayerStatistic { MatchId = match3.Id, ParticipantId = participantC.Id, Average = 80, AverageDartsPerLeg = 19, CheckoutPercent = 32 },
            new MatchPlayerStatistic { MatchId = match3.Id, ParticipantId = participantA.Id, Average = 90, AverageDartsPerLeg = 18, CheckoutPercent = 36 }
        );

        db.ScoringCriteria.AddRange(
            new ScoringCriterion { TournamentId = tournamentId, Type = ScoringCriterionType.Points, Priority = 1, IsEnabled = true },
            new ScoringCriterion { TournamentId = tournamentId, Type = ScoringCriterionType.DirectDuel, Priority = 2, IsEnabled = true },
            new ScoringCriterion { TournamentId = tournamentId, Type = ScoringCriterionType.Average, Priority = 3, IsEnabled = true }
        );

        await db.SaveChangesAsync();

        var standings = await service.GetGroupStandingsAsync(tournamentId);
        var group = standings.Where(x => x.GroupNumber == 1).OrderBy(x => x.Rank).ToList();

        group.Select(x => x.ParticipantName).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
        group.Select(x => x.Rank).Should().ContainInOrder(1, 2, 3);
        group.Should().OnlyContain(x => x.TiebreakerApplied == "Average");
    }
}
