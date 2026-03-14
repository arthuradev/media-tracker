using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.Services.Providers;

public class RawgProvider : IMetadataProvider
{
    private const string BaseUrl = "https://api.rawg.io/api";
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DetailCacheDuration = TimeSpan.FromHours(6);

    private readonly ResilientHttpService _http;
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private readonly ILogger<RawgProvider> _logger;

    public string Name => "RAWG";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    public string ConfigurationHint => _localization.Get("provider.rawg.configHint");
    public MediaType[] SupportedTypes => [MediaType.Game];
    private string ApiKey => _settings.RawgApiKey?.Trim() ?? string.Empty;

    public RawgProvider(ResilientHttpService http, AppSettings settings, LocalizationService localization, ILogger<RawgProvider> logger)
    {
        _http = http;
        _settings = settings;
        _localization = localization;
        _logger = logger;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
    {
        if (!IsConfigured || mediaType != MediaType.Game || string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            string url = $"{BaseUrl}/games?key={ApiKey}&search={Uri.EscapeDataString(query)}&page_size=15";
            string cacheKey = $"rawg:search:{query.Trim().ToLowerInvariant()}";

            var response = await _http.GetJsonAsync<RawgSearchResponse>(url, cacheKey, SearchCacheDuration, ct);

            return response?.Results?.Select(g => new SearchResult
            {
                ExternalId = g.Id.ToString(),
                Title = g.Name ?? string.Empty,
                MediaType = MediaType.Game,
                ReleaseYear = ParseYear(g.Released),
                CoverImageUrl = g.BackgroundImage,
                Genres = string.Join(", ", g.Genres?.Select(x => x.Name) ?? []),
                ProviderName = Name,
                ExternalUrl = $"https://rawg.io/games/{g.Slug}"
            }).ToList() ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RAWG search failed for query {Query}", query);
            return [];
        }
    }

    public async Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default)
    {
        if (!IsConfigured || mediaType != MediaType.Game || string.IsNullOrWhiteSpace(externalId))
            return null;

        try
        {
            string url = $"{BaseUrl}/games/{externalId}?key={ApiKey}";
            string cacheKey = $"rawg:detail:{externalId}";
            var game = await _http.GetJsonAsync<RawgGameDetail>(url, cacheKey, DetailCacheDuration, ct);

            if (game is null)
                return null;

            return new SearchResult
            {
                ExternalId = game.Id.ToString(),
                Title = game.Name ?? string.Empty,
                MediaType = MediaType.Game,
                ReleaseYear = ParseYear(game.Released),
                Synopsis = StripHtml(game.Description),
                CoverImageUrl = game.BackgroundImage,
                BackdropImageUrl = game.BackgroundImageAdditional,
                Genres = string.Join(", ", game.Genres?.Select(x => x.Name) ?? []),
                ProviderName = Name,
                ExternalUrl = $"https://rawg.io/games/{game.Slug}"
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RAWG detail lookup failed for external id {ExternalId}", externalId);
            return null;
        }
    }

    public Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default)
        => Task.FromResult(new List<EpisodeResult>());

    private static int? ParseYear(string? date) =>
        DateTime.TryParse(date, out var parsed) ? parsed.Year : null;

    private static string? StripHtml(string? html)
    {
        if (html is null)
            return null;

        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty).Trim();
    }

    private class RawgSearchResponse
    {
        [JsonPropertyName("results")] public List<RawgGame>? Results { get; set; }
    }

    private class RawgGame
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; }
        [JsonPropertyName("released")] public string? Released { get; set; }
        [JsonPropertyName("background_image")] public string? BackgroundImage { get; set; }
        [JsonPropertyName("genres")] public List<RawgGenre>? Genres { get; set; }
    }

    private class RawgGameDetail
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; }
        [JsonPropertyName("released")] public string? Released { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("background_image")] public string? BackgroundImage { get; set; }
        [JsonPropertyName("background_image_additional")] public string? BackgroundImageAdditional { get; set; }
        [JsonPropertyName("genres")] public List<RawgGenre>? Genres { get; set; }
    }

    private class RawgGenre
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }
}
