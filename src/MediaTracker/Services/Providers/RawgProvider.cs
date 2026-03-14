using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.Services.Providers;

public class RawgProvider : IMetadataProvider
{
    private const string BaseUrl = "https://api.rawg.io/api";
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DetailCacheDuration = TimeSpan.FromHours(6);
    private static readonly HashSet<string> KnownLanguageHeadings =
    [
        "english",
        "ingles",
        "portuguese",
        "portugues",
        "portugues do brasil",
        "portugues brasileiro",
        "pt br",
        "pt-br",
        "spanish",
        "espanol",
        "castellano",
        "french",
        "francais",
        "german",
        "deutsch",
        "italian",
        "italiano"
    ];

    private static readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> GenreMaps =
        new Dictionary<AppLanguage, IReadOnlyDictionary<string, string>>
        {
            [AppLanguage.PortugueseBrazil] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"Action", "Ação"}, {"Adventure", "Aventura"}, {"RPG", "RPG"}, {"Strategy", "Estratégia"},
                {"Shooter", "Tiro"}, {"Puzzle", "Quebra-cabeça"}, {"Platformer", "Plataforma"},
                {"Racing", "Corrida"}, {"Sports", "Esportes"}, {"Fighting", "Luta"},
                {"Simulation", "Simulação"}, {"Arcade", "Arcade"}, {"Indie", "Indie"},
                {"Casual", "Casual"}, {"Massively Multiplayer", "MMO"}, {"Family", "Família"},
                {"Board Games", "Jogos de tabuleiro"}, {"Card", "Cartas"}, {"Educational", "Educativo"}
            },
            [AppLanguage.Spanish] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"Action", "Acción"}, {"Adventure", "Aventura"}, {"RPG", "RPG"}, {"Strategy", "Estrategia"},
                {"Shooter", "Disparos"}, {"Puzzle", "Rompecabezas"}, {"Platformer", "Plataformas"},
                {"Racing", "Carreras"}, {"Sports", "Deportes"}, {"Fighting", "Lucha"},
                {"Simulation", "Simulación"}, {"Arcade", "Arcade"}, {"Indie", "Indie"},
                {"Casual", "Casual"}, {"Massively Multiplayer", "MMO"}, {"Family", "Familia"},
                {"Board Games", "Juegos de mesa"}, {"Card", "Cartas"}, {"Educational", "Educativo"}
            }
        };

    private readonly ResilientHttpService _http;
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private readonly DeepLTranslationService _translation;
    private readonly ILogger<RawgProvider> _logger;

    public string Name => "RAWG";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    public string ConfigurationHint => _localization.Get("provider.rawg.configHint");
    public MediaType[] SupportedTypes => [MediaType.Game];
    private string ApiKey => _settings.RawgApiKey?.Trim() ?? string.Empty;

    public RawgProvider(ResilientHttpService http, AppSettings settings, LocalizationService localization, DeepLTranslationService translation, ILogger<RawgProvider> logger)
    {
        _http = http;
        _settings = settings;
        _localization = localization;
        _translation = translation;
        _logger = logger;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
    {
        if (!IsConfigured || mediaType != MediaType.Game || string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            string url = $"{BaseUrl}/games?key={ApiKey}&search={Uri.EscapeDataString(query)}&page_size=15";
            string cacheKey = $"rawg:search:{_localization.CurrentLanguageCode}:{query.Trim().ToLowerInvariant()}";

            var response = await _http.GetJsonAsync<RawgSearchResponse>(
                url,
                cacheKey,
                SearchCacheDuration,
                ct,
                configureRequest: ApplyLanguageHeaders);

            return response?.Results?.Select(g => new SearchResult
            {
                ExternalId = g.Id.ToString(),
                Title = g.Name ?? string.Empty,
                MediaType = MediaType.Game,
                ReleaseYear = ParseYear(g.Released),
                CoverImageUrl = g.BackgroundImage,
                Genres = MapGenres(g.Genres),
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
            string cacheKey = $"rawg:detail:{_localization.CurrentLanguageCode}:{externalId}";
            var game = await _http.GetJsonAsync<RawgGameDetail>(
                url,
                cacheKey,
                DetailCacheDuration,
                ct,
                configureRequest: ApplyLanguageHeaders);

            if (game is null)
                return null;

            string? synopsis = BuildSynopsis(game);
            synopsis = await TranslateSynopsisAsync(synopsis, externalId, ct);

            return new SearchResult
            {
                ExternalId = game.Id.ToString(),
                Title = game.Name ?? string.Empty,
                MediaType = MediaType.Game,
                ReleaseYear = ParseYear(game.Released),
                Synopsis = synopsis,
                CoverImageUrl = game.BackgroundImage,
                BackdropImageUrl = game.BackgroundImageAdditional,
                Genres = MapGenres(game.Genres),
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

    private string MapGenres(List<RawgGenre>? genres)
    {
        if (genres is null || genres.Count == 0)
            return string.Empty;

        if (!GenreMaps.TryGetValue(_localization.CurrentLanguage, out var genreMap))
            return string.Join(", ", genres.Select(g => g.Name));

        return string.Join(", ", genres.Select(g =>
            genreMap.TryGetValue(g.Name, out var localized) ? localized : g.Name));
    }

    private async Task<string?> TranslateSynopsisAsync(string? synopsis, string externalId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(synopsis) || !_translation.IsConfigured)
            return synopsis;

        if (_localization.CurrentLanguage == AppLanguage.English)
            return synopsis;

        try
        {
            string? translated = await _translation.TranslateAsync(synopsis, ct);
            return translated ?? synopsis;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Synopsis translation failed for game {ExternalId}", externalId);
            return synopsis;
        }
    }

    private string? BuildSynopsis(RawgGameDetail game)
    {
        string? description = CleanSynopsisText(game.DescriptionRaw);
        if (description is null)
            description = CleanSynopsisText(StripHtml(game.Description));

        return SelectBestLanguageBlock(description);
    }

    private string? SelectBestLanguageBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var sections = SplitLanguageSections(text);
        if (sections.Count <= 1)
            return text;

        foreach (string preferredHeading in GetPreferredLanguageHeadings())
        {
            var preferredSection = sections.FirstOrDefault(section =>
                section.Header is not null &&
                string.Equals(NormalizeHeading(section.Header), preferredHeading, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(preferredSection.Content))
                return preferredSection.Content;
        }

        foreach (string fallbackHeading in GetFallbackLanguageHeadings())
        {
            var fallbackSection = sections.FirstOrDefault(section =>
                section.Header is not null &&
                string.Equals(NormalizeHeading(section.Header), fallbackHeading, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(fallbackSection.Content))
                return fallbackSection.Content;
        }

        var defaultSection = sections.FirstOrDefault(section => section.Header is null);
        if (!string.IsNullOrWhiteSpace(defaultSection.Content))
            return defaultSection.Content;

        return sections[0].Content;
    }

    private List<string> GetPreferredLanguageHeadings() => _localization.CurrentLanguage switch
    {
        AppLanguage.PortugueseBrazil => ["portugues", "portugues do brasil", "portugues brasileiro", "portuguese", "pt br", "pt-br"],
        AppLanguage.Spanish => ["espanol", "spanish", "castellano"],
        _ => ["english", "ingles"]
    };

    private static List<string> GetFallbackLanguageHeadings() => ["english", "ingles"];

    private static List<LanguageSection> SplitLanguageSections(string text)
    {
        var sections = new List<LanguageSection>();
        string? currentHeader = null;
        var currentLines = new List<string>();

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (IsLanguageHeading(line))
            {
                AddLanguageSection(sections, currentHeader, currentLines);
                currentHeader = line;
                currentLines = [];
                continue;
            }

            currentLines.Add(rawLine.TrimEnd());
        }

        AddLanguageSection(sections, currentHeader, currentLines);
        return sections;
    }

    private static void AddLanguageSection(List<LanguageSection> sections, string? header, List<string> lines)
    {
        string content = Regex.Replace(string.Join('\n', lines).Trim(), @"\n{3,}", "\n\n");
        if (string.IsNullOrWhiteSpace(content))
            return;

        sections.Add(new LanguageSection(header, content));
    }

    private static bool IsLanguageHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 32)
            return false;

        return KnownLanguageHeadings.Contains(NormalizeHeading(line.Trim(':')));
    }

    private static string NormalizeHeading(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.ToLowerInvariant(ch));
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static string? CleanSynopsisText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string cleaned = WebUtility.HtmlDecode(text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        cleaned = Regex.Replace(cleaned, @"[ \t]+\n", "\n");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        string text = Regex.Replace(html, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/\s*(p|div|h[1-6]|li)\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*li[^>]*>", "- ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<.*?>", string.Empty, RegexOptions.Singleline);

        return text;
    }

    private void ApplyLanguageHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Accept-Language", GetAcceptLanguageHeader());
    }

    private string GetAcceptLanguageHeader() => _localization.CurrentLanguage switch
    {
        AppLanguage.PortugueseBrazil => "pt-BR,pt;q=0.9,en-US;q=0.7,en;q=0.6",
        AppLanguage.Spanish => "es-ES,es;q=0.9,en-US;q=0.7,en;q=0.6",
        _ => "en-US,en;q=0.9"
    };

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
        [JsonPropertyName("description_raw")] public string? DescriptionRaw { get; set; }
        [JsonPropertyName("background_image")] public string? BackgroundImage { get; set; }
        [JsonPropertyName("background_image_additional")] public string? BackgroundImageAdditional { get; set; }
        [JsonPropertyName("genres")] public List<RawgGenre>? Genres { get; set; }
    }

    private class RawgGenre
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private readonly record struct LanguageSection(string? Header, string Content);
}
