using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;

namespace MediaTracker.BasicTests;

public sealed class RawgProviderTests
{
    [Fact]
    public async Task DetailCleaningPicksTheBestLanguageBlockWithoutBreakingCachedResults()
    {
        int calls = 0;
        var requestLanguages = new List<string>();
        using var handler = new SequenceHttpMessageHandler(request =>
        {
            calls++;
            requestLanguages.Add(string.Join(",", request.Headers.GetValues("Accept-Language")));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": 7,
                      "name": "Grand Theft Auto V",
                      "slug": "grand-theft-auto-v",
                      "released": "2013-09-17",
                      "description_raw": "Rockstar Games went bigger.\n\nFollow Michael, Franklin and Trevor.\n\nEspanol\nRockstar Games se hizo mas grande.\n\nSigue a Michael, Franklin y Trevor.",
                      "background_image": "https://example.test/gta-cover.jpg",
                      "background_image_additional": "https://example.test/gta-backdrop.jpg",
                      "genres": [{ "name": "Action" }]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());
        var settings = new AppSettings { RawgApiKey = "rawg-key", PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);
        var provider = new RawgProvider(http, settings, localization, new DeepLTranslationService(http, settings, localization, new FakeLogger<DeepLTranslationService>()), new FakeLogger<RawgProvider>());

        var english = await provider.GetDetailsAsync("7", MediaType.Game);

        localization.SetLanguage(AppLanguage.Spanish);
        var spanish = await provider.GetDetailsAsync("7", MediaType.Game);

        Assert.Equal(2, calls);
        Assert.Contains("en-US", requestLanguages[0], StringComparison.Ordinal);
        Assert.Contains("es-ES", requestLanguages[1], StringComparison.Ordinal);
        Assert.Equal("Rockstar Games went bigger.\n\nFollow Michael, Franklin and Trevor.", english?.Synopsis);
        Assert.Equal("Rockstar Games se hizo mas grande.\n\nSigue a Michael, Franklin y Trevor.", spanish?.Synopsis);
        Assert.DoesNotContain("Espanol", spanish?.Synopsis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetailFallsBackToEnglishBlockWhenSelectedLanguageIsUnavailable()
    {
        using var handler = new SequenceHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": 9,
                      "name": "Game With Sections",
                      "slug": "game-with-sections",
                      "released": "2024-01-01",
                      "description_raw": "English\nEnglish description.\n\nFrench\nDescription francaise.",
                      "background_image": "https://example.test/game-cover.jpg",
                      "background_image_additional": "https://example.test/game-backdrop.jpg",
                      "genres": [{ "name": "Action" }]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());
        var settings = new AppSettings { RawgApiKey = "rawg-key", PreferredLanguage = AppLanguage.PortugueseBrazil };
        var localization = new LocalizationService(settings);
        var provider = new RawgProvider(http, settings, localization, new DeepLTranslationService(http, settings, localization, new FakeLogger<DeepLTranslationService>()), new FakeLogger<RawgProvider>());

        var details = await provider.GetDetailsAsync("9", MediaType.Game);

        Assert.Equal("English description.", details?.Synopsis);
    }
}
