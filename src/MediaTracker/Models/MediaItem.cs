namespace MediaTracker.Models;

public class MediaItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public MediaType MediaType { get; set; }
    public int? ReleaseYear { get; set; }
    public string? Synopsis { get; set; }
    public string? CoverImagePath { get; set; }
    public string? BackdropImagePath { get; set; }
    public string? Genres { get; set; }
    public MediaStatus Status { get; set; } = MediaStatus.PlanToWatch;
    public int? UserScore { get; set; }
    public string? UserReview { get; set; }
    public int? TotalEpisodes { get; set; }
    public int? TotalSeasons { get; set; }
    public int? RuntimeMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAt { get; set; }

    public ICollection<Episode> Episodes { get; set; } = [];
    public ICollection<ProviderMapping> ProviderMappings { get; set; } = [];
    public GameProgress? GameProgress { get; set; }
}
