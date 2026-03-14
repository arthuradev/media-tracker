using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;

namespace MediaTracker.BasicTests;

public sealed class TmdbProviderTests
{
    [Fact]
    public async Task SearchUsesSelectedLanguageAndLocaleSpecificCacheKeys()
    {
        var requestedUris = new List<string>();
        using var handler = new SequenceHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"results":[{"id":1,"title":"Batman","original_title":"The Batman","overview":"Test","release_date":"2022-03-01","genre_ids":[28,12]}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());
        var settings = new AppSettings { TmdbApiKey = "tmdb-key", PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);
        var provider = new TmdbProvider(http, settings, localization, new FakeLogger<TmdbProvider>());

        var englishResults = await provider.SearchAsync("Batman", MediaType.Movie);

        localization.SetLanguage(AppLanguage.PortugueseBrazil);
        var portugueseResults = await provider.SearchAsync("Batman", MediaType.Movie);

        Assert.Equal(2, requestedUris.Count);
        Assert.Contains("language=en-US", requestedUris[0], StringComparison.Ordinal);
        Assert.Contains("language=pt-BR", requestedUris[1], StringComparison.Ordinal);
        Assert.Equal("Action, Adventure", englishResults.Single().Genres);
        Assert.Equal("Ação, Aventura", portugueseResults.Single().Genres);
    }

    [Fact]
    public async Task DetailAndEpisodesUseCurrentLocale()
    {
        var requestedUris = new List<string>();
        using var handler = new SequenceHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!.ToString());

            string payload = request.RequestUri!.AbsolutePath.Contains("/season/", StringComparison.Ordinal)
                ? """{"episodes":[{"episode_number":1,"name":"Pilot","overview":"Test","air_date":"2024-01-01","runtime":42}]}"""
                : """{"id":1,"title":"Batman","original_title":"The Batman","overview":"Test","release_date":"2022-03-01","runtime":176,"genres":[{"name":"Accion"}]}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());
        var settings = new AppSettings { TmdbApiKey = "tmdb-key", PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);
        var provider = new TmdbProvider(http, settings, localization, new FakeLogger<TmdbProvider>());

        localization.SetLanguage(AppLanguage.Spanish);

        await provider.GetDetailsAsync("1", MediaType.Movie);
        await provider.GetEpisodesAsync("1", 1);

        Assert.Equal(2, requestedUris.Count);
        Assert.All(requestedUris, uri => Assert.Contains("language=es-ES", uri, StringComparison.Ordinal));
    }
}
