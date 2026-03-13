using MediaTracker.Models;

namespace MediaTracker.Services.Providers;

public interface IMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    string ConfigurationHint { get; }
    MediaType[] SupportedTypes { get; }
    Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default);
    Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default);
    Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default);
}

public class EpisodeResult
{
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string? Title { get; set; }
    public string? Overview { get; set; }
    public DateOnly? AirDate { get; set; }
    public int? Runtime { get; set; }
}
