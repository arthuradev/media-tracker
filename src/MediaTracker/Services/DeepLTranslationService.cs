using System.Net.Http;
using System.Text.Json.Serialization;
using MediaTracker.Models;
using Microsoft.Extensions.Logging;

namespace MediaTracker.Services;

public class DeepLTranslationService
{
    private static readonly TimeSpan TranslationCacheDuration = TimeSpan.FromDays(30);

    private readonly ResilientHttpService _http;
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private readonly ILogger<DeepLTranslationService> _logger;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.DeepLApiKey?.Trim());

    public DeepLTranslationService(
        ResilientHttpService http,
        AppSettings settings,
        LocalizationService localization,
        ILogger<DeepLTranslationService> logger)
    {
        _http = http;
        _settings = settings;
        _localization = localization;
        _logger = logger;
    }

    public async Task<string?> TranslateAsync(string text, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text))
            return null;

        string targetLang = GetDeepLTargetLanguage();
        if (targetLang == "EN")
            return null;

        string cacheKey = $"deepl:{targetLang}:{text.GetHashCode():X8}";

        try
        {
            string apiKey = _settings.DeepLApiKey.Trim();
            string baseUrl = apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";

            var formData = new Dictionary<string, string>
            {
                ["text"] = text,
                ["target_lang"] = targetLang,
                ["source_lang"] = "EN"
            };

            var result = await _http.PostFormAsync<DeepLResponse>(
                baseUrl,
                formData,
                cacheKey,
                TranslationCacheDuration,
                ct,
                configureRequest: request =>
                {
                    request.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {apiKey}");
                });

            string? translated = result?.Translations?.FirstOrDefault()?.Text;
            if (!string.IsNullOrWhiteSpace(translated))
                return translated;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeepL translation failed");
        }

        return null;
    }

    private string GetDeepLTargetLanguage() => _localization.CurrentLanguage switch
    {
        AppLanguage.PortugueseBrazil => "PT-BR",
        AppLanguage.Spanish => "ES",
        _ => "EN"
    };

    private class DeepLResponse
    {
        [JsonPropertyName("translations")]
        public List<DeepLTranslation>? Translations { get; set; }
    }

    private class DeepLTranslation
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
