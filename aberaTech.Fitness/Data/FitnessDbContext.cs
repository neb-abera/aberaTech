using Microsoft.EntityFrameworkCore;

namespace aberaTech.Fitness.Data;

public class FitnessDbContext(DbContextOptions<FitnessDbContext> options) : DbContext(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<StrengthSet> StrengthSets => Set<StrengthSet>();
    public DbSet<BodyMetric> BodyMetrics => Set<BodyMetric>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<AthleteSettings> Settings => Set<AthleteSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Idempotent imports: the same external record lands on the same row.
        modelBuilder.Entity<Activity>()
            .HasIndex(a => new { a.Source, a.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");

        modelBuilder.Entity<Activity>()
            .HasIndex(a => a.StartedAt);

        modelBuilder.Entity<Activity>()
            .HasMany(a => a.Sets)
            .WithOne()
            .HasForeignKey(s => s.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        // One weigh-in per day; a second entry that day is a correction.
        modelBuilder.Entity<BodyMetric>()
            .HasIndex(m => m.Date)
            .IsUnique();

        modelBuilder.Entity<Goal>()
            .HasIndex(g => g.Metric)
            .IsUnique();

        modelBuilder.Entity<Activity>().Property(a => a.Source).HasMaxLength(32);
        modelBuilder.Entity<Activity>().Property(a => a.ExternalId).HasMaxLength(128);
        modelBuilder.Entity<Activity>().Property(a => a.Sport).HasMaxLength(16);
        modelBuilder.Entity<Activity>().Property(a => a.Name).HasMaxLength(256);
        modelBuilder.Entity<StrengthSet>().Property(s => s.Exercise).HasMaxLength(128);
        modelBuilder.Entity<Goal>().Property(g => g.Metric).HasMaxLength(64);
        modelBuilder.Entity<Goal>().Property(g => g.Label).HasMaxLength(128);
    }
}
