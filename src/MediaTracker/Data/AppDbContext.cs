using Microsoft.EntityFrameworkCore;
using MediaTracker.Models;

namespace MediaTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<GameProgress> GameProgresses => Set<GameProgress>();
    public DbSet<ProviderMapping> ProviderMappings => Set<ProviderMapping>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OriginalTitle).HasMaxLength(500);
            entity.Property(e => e.Genres).HasMaxLength(500);
            entity.Property(e => e.MediaType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.MediaType);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Episode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.MediaItem)
                  .WithMany(m => m.Episodes)
                  .HasForeignKey(e => e.MediaItemId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.MediaItemId, e.SeasonNumber, e.EpisodeNumber }).IsUnique();
        });

        modelBuilder.Entity<GameProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.MediaItem)
                  .WithOne(m => m.GameProgress)
                  .HasForeignKey<GameProgress>(e => e.MediaItemId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CompletionState).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ProviderMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.MediaItem)
                  .WithMany(m => m.ProviderMappings)
                  .HasForeignKey(e => e.MediaItemId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.ProviderName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.MediaItemId, e.ProviderName }).IsUnique();
        });
    }
}
