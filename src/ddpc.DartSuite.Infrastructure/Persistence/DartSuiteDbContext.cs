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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(entity =>
        {
            entity.HasIndex(x => x.ExternalBoardId).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.CurrentMatchLabel).HasMaxLength(256);
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.JoinCode).HasMaxLength(3);
            entity.HasIndex(x => x.JoinCode).IsUnique().HasFilter("[JoinCode] IS NOT NULL");
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
    }
}