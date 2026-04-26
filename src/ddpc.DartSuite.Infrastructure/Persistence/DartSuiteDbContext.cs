using ddpc.DartSuite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Persistence;

public sealed class DartSuiteDbContext(DbContextOptions<DartSuiteDbContext> options) : DbContext(options)
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<TournamentRound> TournamentRounds => Set<TournamentRound>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<ScoringCriterion> ScoringCriteria => Set<ScoringCriterion>();
    public DbSet<MatchPlayerStatistic> MatchPlayerStatistics => Set<MatchPlayerStatistic>();
    public DbSet<NotificationSubscription> NotificationSubscriptions => Set<NotificationSubscription>();
    public DbSet<MatchFollower> MatchFollowers => Set<MatchFollower>();
    public DbSet<UserViewPreference> UserViewPreferences => Set<UserViewPreference>();
    public DbSet<Admin> Admins => Set<Admin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(entity =>
        {
            //entity.HasIndex(x => x.ExternalBoardId).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.CurrentMatchLabel).HasMaxLength(256);
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.JoinCode).HasMaxLength(3);
            // Provider-aware index filter for JoinCode
            var provider = Database.ProviderName;
            if (provider != null && provider.ToLower().Contains("npgsql"))
            {
                entity.HasIndex(x => x.JoinCode).IsUnique().HasFilter("\"JoinCode\" IS NOT NULL");
            }
            else
            {
                entity.HasIndex(x => x.JoinCode).IsUnique().HasFilter("[JoinCode] IS NOT NULL");
            }
            entity.Ignore(x => x.Participants);
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasIndex(x => new { x.TournamentId, x.AccountName }).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(128);
            entity.Property(x => x.AccountName).HasMaxLength(128);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasIndex(x => new { x.TournamentId, x.Phase, x.Round, x.MatchNumber }).IsUnique();
        });

        modelBuilder.Entity<TournamentRound>(entity =>
        {
            entity.HasIndex(x => new { x.TournamentId, x.Phase, x.RoundNumber }).IsUnique();
            entity.Property(x => x.InMode).HasMaxLength(32);
            entity.Property(x => x.OutMode).HasMaxLength(32);
            entity.Property(x => x.BullMode).HasMaxLength(32);
            entity.Property(x => x.BullOffMode).HasMaxLength(32);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.HasIndex(x => new { x.TournamentId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ScoringCriterion>(entity =>
        {
            entity.HasIndex(x => new { x.TournamentId, x.Type }).IsUnique();
        });

        modelBuilder.Entity<MatchPlayerStatistic>(entity =>
        {
            entity.HasIndex(x => new { x.MatchId, x.ParticipantId }).IsUnique();
        });

        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.HasIndex(x => new { x.TournamentId, x.UserAccountName, x.Endpoint }).IsUnique();
            entity.Property(x => x.UserAccountName).HasMaxLength(128);
            entity.Property(x => x.Endpoint).HasMaxLength(512);
            entity.Property(x => x.P256dh).HasMaxLength(256);
            entity.Property(x => x.Auth).HasMaxLength(256);
        });

        modelBuilder.Entity<MatchFollower>(entity =>
        {
            entity.HasIndex(x => new { x.MatchId, x.UserAccountName }).IsUnique();
            entity.Property(x => x.UserAccountName).HasMaxLength(128);
        });

        modelBuilder.Entity<UserViewPreference>(entity =>
        {
            entity.HasIndex(x => new { x.UserAccountName, x.ViewContext }).IsUnique();
            entity.Property(x => x.UserAccountName).HasMaxLength(128);
            entity.Property(x => x.ViewContext).HasMaxLength(128);
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasIndex(x => x.AccountName).IsUnique();
            entity.Property(x => x.AccountName).HasMaxLength(128).IsRequired();
        });
    }
}