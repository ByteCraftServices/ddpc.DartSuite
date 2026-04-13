using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Persistence;

public static class DartSuiteSeeder
{
    public static async Task SeedAsync(DartSuiteDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await SeedAdminsAsync(dbContext, cancellationToken);

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

        // Knockout Tournament
        var koTournament = new Tournament
        {
            Name = "DartSuite Demo Cup (KO)",
            OrganizerAccount = "manager.demo",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Mode = TournamentMode.Knockout,
            Variant = TournamentVariant.OnSite,
            TeamplayEnabled = false,
            JoinCode = "123"
        };
        dbContext.Tournaments.Add(koTournament);
        dbContext.Participants.AddRange(
            new Participant
            {
                TournamentId = koTournament.Id,
                DisplayName = "Anna",
                AccountName = "anna",
                IsAutodartsAccount = false,
                IsManager = true,
                Seed = 1
            },
            new Participant
            {
                TournamentId = koTournament.Id,
                DisplayName = "Ben",
                AccountName = "ben",
                IsAutodartsAccount = false,
                Seed = 2
            }
        );

        // Group Tournament
        var groupTournament = new Tournament
        {
            Name = "DartSuite Gruppenpokal",
            OrganizerAccount = "manager.demo",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Mode = TournamentMode.GroupAndKnockout,
            Variant = TournamentVariant.OnSite,
            TeamplayEnabled = false,
            JoinCode = "234",
            GroupCount = 2
        };
        dbContext.Tournaments.Add(groupTournament);
        dbContext.Participants.AddRange(
            new Participant
            {
                TournamentId = groupTournament.Id,
                DisplayName = "Clara",
                AccountName = "clara",
                IsAutodartsAccount = false,
                IsManager = true,
                Seed = 1
            },
            new Participant
            {
                TournamentId = groupTournament.Id,
                DisplayName = "David",
                AccountName = "david",
                IsAutodartsAccount = false,
                Seed = 2
            },
            new Participant
            {
                TournamentId = groupTournament.Id,
                DisplayName = "Eva",
                AccountName = "eva",
                IsAutodartsAccount = false,
                Seed = 3
            }
        );

        // Group + KO Tournament
        var groupKoTournament = new Tournament
        {
            Name = "DartSuite Masters (Gruppe + KO)",
            OrganizerAccount = "manager.demo",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Mode = TournamentMode.GroupAndKnockout,
            Variant = TournamentVariant.OnSite,
            TeamplayEnabled = false,
            JoinCode = "345",
            GroupCount = 2
        };
        dbContext.Tournaments.Add(groupKoTournament);
        dbContext.Participants.AddRange(
            new Participant
            {
                TournamentId = groupKoTournament.Id,
                DisplayName = "Felix",
                AccountName = "felix",
                IsAutodartsAccount = false,
                IsManager = true,
                Seed = 1
            },
            new Participant
            {
                TournamentId = groupKoTournament.Id,
                DisplayName = "Greta",
                AccountName = "greta",
                IsAutodartsAccount = false,
                Seed = 2
            },
            new Participant
            {
                TournamentId = groupKoTournament.Id,
                DisplayName = "Hannes",
                AccountName = "hannes",
                IsAutodartsAccount = false,
                Seed = 3
            }
        );

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminsAsync(DartSuiteDbContext dbContext, CancellationToken cancellationToken)
    {
        const string seedAdminAccount = "doc";
        var exists = await dbContext.Admins
            .AnyAsync(a => a.AccountName == seedAdminAccount, cancellationToken);

        if (!exists)
        {
            dbContext.Admins.Add(new Admin
            {
                AccountName = seedAdminAccount,
                ValidFromDate = new DateOnly(2000, 1, 1),
                ValidToDate = DateOnly.MaxValue
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}