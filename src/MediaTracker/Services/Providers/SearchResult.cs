using MediaTracker.Models;

namespace MediaTracker.Services.Providers;

public class SearchResult
{
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public MediaType MediaType { get; set; }
    public int? ReleaseYear { get; set; }
    public string? Synopsis { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? BackdropImageUrl { get; set; }
    public string? Genres { get; set; }
    public int? TotalEpisodes { get; set; }
    public int? TotalSeasons { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
}
