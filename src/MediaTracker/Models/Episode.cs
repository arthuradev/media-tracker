namespace MediaTracker.Models;

public class Episode
{
    public int Id { get; set; }
    public int MediaItemId { get; set; }
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string? Title { get; set; }
    public string? Overview { get; set; }
    public DateOnly? AirDate { get; set; }
    public int? Runtime { get; set; }
    public bool IsWatched { get; set; }
    public DateTime? WatchedAt { get; set; }
    public int? UserScore { get; set; }

    public MediaItem MediaItem { get; set; } = null!;
}
