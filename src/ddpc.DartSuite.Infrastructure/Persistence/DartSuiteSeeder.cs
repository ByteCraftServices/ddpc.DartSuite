using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Infrastructure.Persistence;

public static class DartSuiteSeeder
{
    public static async Task SeedAsync(DartSuiteDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (dbContext.Boards.Any())
        {
            return;
        }

        dbContext.Boards.AddRange(
            new Board
            {
                ExternalBoardId = "board-alpha",
                Name = "Manual",
                LocalIpAddress = "127.0.0.1",
                BoardManagerUrl = "http://127.0.0.1:3180",
                Status = BoardStatus.Offline
            }
        );

        var tournament = new Tournament
        {
            Name = "DartSuite Demo Cup",
            OrganizerAccount = "manager.demo",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Mode = TournamentMode.Knockout,
            Variant = TournamentVariant.OnSite,
            TeamplayEnabled = false,
            JoinCode = "123"
        };

        dbContext.Tournaments.Add(tournament);
        dbContext.Participants.AddRange(
            new Participant
            {
                TournamentId = tournament.Id,
                DisplayName = "Anna",
                AccountName = "anna",
                IsAutodartsAccount = false,
                IsManager = true,
                Seed = 1
            },
            new Participant
            {
                TournamentId = tournament.Id,
                DisplayName = "Ben",
                AccountName = "ben",
                IsAutodartsAccount = false,
                Seed = 2
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}