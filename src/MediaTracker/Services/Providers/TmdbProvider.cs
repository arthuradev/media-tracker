using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.Services.Providers;

public class TmdbProvider : IMetadataProvider
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p";

    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DetailCacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan EpisodeCacheDuration = TimeSpan.FromHours(12);

    private readonly ResilientHttpService _http;
    private readonly AppSettings _settings;
    private readonly ILogger<TmdbProvider> _logger;

    private static readonly Dictionary<int, string> GenreMap = new()
    {
        {28, "Action"}, {12, "Adventure"}, {16, "Animation"}, {35, "Comedy"},
        {80, "Crime"}, {99, "Documentary"}, {18, "Drama"}, {10751, "Family"},
        {14, "Fantasy"}, {36, "History"}, {27, "Horror"}, {10402, "Music"},
        {9648, "Mystery"}, {10749, "Romance"}, {878, "Sci-Fi"}, {10770, "TV Movie"},
        {53, "Thriller"}, {10752, "War"}, {37, "Western"},
        {10759, "Action & Adventure"}, {10762, "Kids"}, {10763, "News"},
        {10764, "Reality"}, {10765, "Sci-Fi & Fantasy"}, {10766, "Soap"},
        {10767, "Talk"}, {10768, "War & Politics"}
    };

    public string Name => "TMDB";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    public string ConfigurationHint => "Add a TMDB API key in Settings to search movies, series and anime.";
    public MediaType[] SupportedTypes => [MediaType.Movie, MediaType.Series, MediaType.Anime];
    private string ApiKey => _settings.TmdbApiKey?.Trim() ?? string.Empty;

    public TmdbProvider(ResilientHttpService http, AppSettings settings, ILogger<TmdbProvider> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            if (mediaType == MediaType.Movie)
            {
                string url = $"{BaseUrl}/search/movie?api_key={ApiKey}&query={Uri.EscapeDataString(query)}&language=pt-BR";
                string cacheKey = $"tmdb:search:movie:{query.Trim().ToLowerInvariant()}";
                var response = await _http.GetJsonAsync<TmdbSearchResponse<TmdbMovie>>(url, cacheKey, SearchCacheDuration, ct);

                return response?.Results?.Select(m => new SearchResult
                {
                    ExternalId = m.Id.ToString(),
                    Title = m.Title ?? string.Empty,
                    OriginalTitle = m.OriginalTitle,
                    MediaType = MediaType.Movie,
                    ReleaseYear = ParseYear(m.ReleaseDate),
                    Synopsis = m.Overview,
                    CoverImageUrl = PosterUrl(m.PosterPath),
                    BackdropImageUrl = BackdropUrl(m.BackdropPath),
                    Genres = MapGenres(m.GenreIds),
                    ProviderName = Name,
                    ExternalUrl = $"https://www.themoviedb.org/movie/{m.Id}"
                }).ToList() ?? [];
            }

            string tvUrl = $"{BaseUrl}/search/tv?api_key={ApiKey}&query={Uri.EscapeDataString(query)}&language=pt-BR";
            string tvCacheKey = $"tmdb:search:tv:{query.Trim().ToLowerInvariant()}";
            var tvResponse = await _http.GetJsonAsync<TmdbSearchResponse<TmdbTv>>(tvUrl, tvCacheKey, SearchCacheDuration, ct);

            var results = tvResponse?.Results?.Select(t => new SearchResult
            {
                ExternalId = t.Id.ToString(),
                Title = t.Name ?? string.Empty,
                OriginalTitle = t.OriginalName,
                MediaType = IsAnime(t) ? MediaType.Anime : MediaType.Series,
                ReleaseYear = ParseYear(t.FirstAirDate),
                Synopsis = t.Overview,
                CoverImageUrl = PosterUrl(t.PosterPath),
                BackdropImageUrl = BackdropUrl(t.BackdropPath),
                Genres = MapGenres(t.GenreIds),
                ProviderName = Name,
                ExternalUrl = $"https://www.themoviedb.org/tv/{t.Id}"
            }).ToList() ?? [];

            return mediaType == MediaType.Anime
                ? results.Where(r => r.MediaType == MediaType.Anime).ToList()
                : results.Where(r => r.MediaType == MediaType.Series).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TMDB search failed for query {Query} and media type {MediaType}", query, mediaType);
            return [];
        }
    }

    public async Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(externalId))
            return null;

        try
        {
            if (mediaType == MediaType.Movie)
            {
                string url = $"{BaseUrl}/movie/{externalId}?api_key={ApiKey}&language=pt-BR";
                var movie = await _http.GetJsonAsync<TmdbMovieDetail>(
                    url,
                    $"tmdb:detail:movie:{externalId}",
                    DetailCacheDuration,
                    ct);

                if (movie is null)
                    return null;

                return new SearchResult
                {
                    ExternalId = movie.Id.ToString(),
                    Title = movie.Title ?? string.Empty,
                    OriginalTitle = movie.OriginalTitle,
                    MediaType = MediaType.Movie,
                    ReleaseYear = ParseYear(movie.ReleaseDate),
                    Synopsis = movie.Overview,
                    CoverImageUrl = PosterUrl(movie.PosterPath),
                    BackdropImageUrl = BackdropUrl(movie.BackdropPath),
                    Genres = string.Join(", ", movie.Genres?.Select(g => g.Name) ?? []),
                    RuntimeMinutes = movie.Runtime,
                    ProviderName = Name,
                    ExternalUrl = $"https://www.themoviedb.org/movie/{movie.Id}"
                };
            }

            string tvUrl = $"{BaseUrl}/tv/{externalId}?api_key={ApiKey}&language=pt-BR";
            var tv = await _http.GetJsonAsync<TmdbTvDetail>(
                tvUrl,
                $"tmdb:detail:tv:{externalId}",
                DetailCacheDuration,
                ct);

            if (tv is null)
                return null;

            return new SearchResult
            {
                ExternalId = tv.Id.ToString(),
                Title = tv.Name ?? string.Empty,
                OriginalTitle = tv.OriginalName,
                MediaType = mediaType,
                ReleaseYear = ParseYear(tv.FirstAirDate),
                Synopsis = tv.Overview,
                CoverImageUrl = PosterUrl(tv.PosterPath),
                BackdropImageUrl = BackdropUrl(tv.BackdropPath),
                Genres = string.Join(", ", tv.Genres?.Select(g => g.Name) ?? []),
                TotalSeasons = tv.NumberOfSeasons,
                TotalEpisodes = tv.NumberOfEpisodes,
                ProviderName = Name,
                ExternalUrl = $"https://www.themoviedb.org/tv/{tv.Id}"
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TMDB detail lookup failed for external id {ExternalId}", externalId);
            return null;
        }
    }

    public async Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(externalId))
            return [];

        try
        {
            string url = $"{BaseUrl}/tv/{externalId}/season/{seasonNumber}?api_key={ApiKey}&language=pt-BR";
            var season = await _http.GetJsonAsync<TmdbSeason>(
                url,
                $"tmdb:episodes:{externalId}:season:{seasonNumber}",
                EpisodeCacheDuration,
                ct);

            return season?.Episodes?.Select(ep => new EpisodeResult
            {
                SeasonNumber = seasonNumber,
                EpisodeNumber = ep.EpisodeNumber,
                Title = ep.Name,
                Overview = ep.Overview,
                AirDate = DateOnly.TryParse(ep.AirDate, out var date) ? date : null,
                Runtime = ep.Runtime
            }).ToList() ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "TMDB episode lookup failed for external id {ExternalId} season {Season}",
                externalId,
                seasonNumber);
            return [];
        }
    }

    private static bool IsAnime(TmdbTv tv) =>
        tv.OriginCountry?.Contains("JP") == true && tv.GenreIds?.Contains(16) == true;

    private static int? ParseYear(string? date) =>
        DateTime.TryParse(date, out var parsed) ? parsed.Year : null;

    private static string? PosterUrl(string? path) =>
        path is not null ? $"{ImageBaseUrl}/w342{path}" : null;

    private static string? BackdropUrl(string? path) =>
        path is not null ? $"{ImageBaseUrl}/w780{path}" : null;

    private static string MapGenres(int[]? ids) =>
        ids is null ? string.Empty : string.Join(", ", ids.Where(GenreMap.ContainsKey).Select(id => GenreMap[id]));

    private class TmdbSearchResponse<T>
    {
        [JsonPropertyName("results")] public List<T>? Results { get; set; }
    }

    private class TmdbMovie
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("original_title")] public string? OriginalTitle { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("genre_ids")] public int[]? GenreIds { get; set; }
    }

    private class TmdbTv
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("original_name")] public string? OriginalName { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("genre_ids")] public int[]? GenreIds { get; set; }
        [JsonPropertyName("origin_country")] public string[]? OriginCountry { get; set; }
    }

    private class TmdbMovieDetail
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("original_title")] public string? OriginalTitle { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("runtime")] public int? Runtime { get; set; }
        [JsonPropertyName("genres")] public List<TmdbGenre>? Genres { get; set; }
    }

    private class TmdbTvDetail
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("original_name")] public string? OriginalName { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
        [JsonPropertyName("number_of_seasons")] public int? NumberOfSeasons { get; set; }
        [JsonPropertyName("number_of_episodes")] public int? NumberOfEpisodes { get; set; }
        [JsonPropertyName("genres")] public List<TmdbGenre>? Genres { get; set; }
    }

    private class TmdbGenre
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private class TmdbSeason
    {
        [JsonPropertyName("episodes")] public List<TmdbEpisode>? Episodes { get; set; }
    }

    private class TmdbEpisode
    {
        [JsonPropertyName("episode_number")] public int EpisodeNumber { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("air_date")] public string? AirDate { get; set; }
        [JsonPropertyName("runtime")] public int? Runtime { get; set; }
    }
}
