using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Caching.Memory;
using MediaTracker.Data;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;
using MediaTracker.ViewModels;

return await TestRunner.RunAsync();

internal static class TestRunner
{
    public static async Task<int> RunAsync()
    {
        var tests = new List<(string Name, Func<Task> Run)>
        {
            ("AddEpisodesAsync skips duplicates", AddEpisodesAsyncSkipsDuplicates),
            ("Progress updates touch parent UpdatedAt", ProgressUpdatesTouchParentTimestamp),
            ("Game progress upserts and updates parent", GameProgressUpsertsAndTouchesParent),
            ("SearchExternalViewModel reports missing provider config", SearchViewModelReportsMissingProviderConfig),
            ("MediaInputValidator rejects empty title", MediaInputValidatorRejectsEmptyTitle),
            ("MediaInputValidator rejects invalid media ranges", MediaInputValidatorRejectsInvalidMediaRanges),
            ("MediaInputValidator rejects negative game hours", MediaInputValidatorRejectsNegativeGameHours),
            ("DetailViewModel clears stale state when load fails", DetailViewModelClearsStaleStateWhenItemMissing),
            ("EpisodesViewModel reports missing provider", EpisodesViewModelReportsMissingProvider),
            ("LibraryViewModel toggles display modes", LibraryViewModelTogglesDisplayModes),
            ("ResilientHttpService caches successful JSON responses", ResilientHttpServiceCachesSuccessfulJsonResponses),
            ("ResilientHttpService retries transient failures", ResilientHttpServiceRetriesTransientFailures),
            ("Providers reflect updated settings without restart", ProvidersReflectUpdatedSettingsWithoutRestart),
            ("AppUpdateService detects newer versions from a local feed", AppUpdateServiceDetectsNewerVersionFromLocalFeed),
            ("AppUpdateService reports when the app is already up to date", AppUpdateServiceReportsUpToDate)
        };

        int failures = 0;

        foreach (var (name, run) in tests)
        {
            try
            {
                await run();
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL  {name}");
                Console.WriteLine($"      {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "All basic robustness tests passed."
            : $"{failures} test(s) failed.");

        return failures == 0 ? 0 : 1;
    }

    private static async Task AddEpisodesAsyncSkipsDuplicates()
    {
        await using var harness = await TestHarness.CreateAsync();
        var item = await harness.MediaService.CreateAsync(new MediaItem
        {
            Title = "Series Test",
            MediaType = MediaType.Series
        });

        int firstInsert = await harness.MediaService.AddEpisodesAsync(
        [
            new Episode { MediaItemId = item.Id, SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" },
            new Episode { MediaItemId = item.Id, SeasonNumber = 1, EpisodeNumber = 2, Title = "Second" }
        ]);

        int secondInsert = await harness.MediaService.AddEpisodesAsync(
        [
            new Episode { MediaItemId = item.Id, SeasonNumber = 1, EpisodeNumber = 2, Title = "Second duplicate" },
            new Episode { MediaItemId = item.Id, SeasonNumber = 1, EpisodeNumber = 3, Title = "Third" }
        ]);

        var episodes = await harness.MediaService.GetEpisodesAsync(item.Id);

        Expect(firstInsert == 2, "Expected first insert to add 2 episodes.");
        Expect(secondInsert == 1, "Expected duplicate insert to add only 1 new episode.");
        Expect(episodes.Count == 3, "Expected exactly 3 stored episodes after duplicate filtering.");
    }

    private static async Task ProgressUpdatesTouchParentTimestamp()
    {
        await using var harness = await TestHarness.CreateAsync();
        var item = await harness.MediaService.CreateAsync(new MediaItem
        {
            Title = "Timestamp Series",
            MediaType = MediaType.Series
        });

        await harness.MediaService.AddEpisodesAsync(
        [
            new Episode { MediaItemId = item.Id, SeasonNumber = 1, EpisodeNumber = 1, Title = "Episode One" }
        ]);

        var before = (await harness.MediaService.GetByIdAsync(item.Id))!.UpdatedAt;
        await Task.Delay(30);

        var episode = (await harness.MediaService.GetEpisodesAsync(item.Id)).Single();
        await harness.MediaService.ToggleEpisodeWatchedAsync(episode.Id);

        var after = await harness.MediaService.GetByIdAsync(item.Id);

        Expect(after is not null, "Expected media item to exist after toggling episode state.");
        Expect(after!.UpdatedAt > before, "Expected UpdatedAt to advance after episode progress changed.");
        Expect(after.Episodes.Single().IsWatched, "Expected the episode to be marked as watched.");
    }

    private static async Task GameProgressUpsertsAndTouchesParent()
    {
        await using var harness = await TestHarness.CreateAsync();
        var item = await harness.MediaService.CreateAsync(new MediaItem
        {
            Title = "Game Test",
            MediaType = MediaType.Game
        });

        var before = (await harness.MediaService.GetByIdAsync(item.Id))!.UpdatedAt;
        await Task.Delay(30);

        await harness.MediaService.UpdateGameProgressAsync(new GameProgress
        {
            MediaItemId = item.Id,
            HoursPlayed = 12.5,
            Platform = "PC",
            CurrentStage = "Chapter 4",
            CompletionState = CompletionState.InProgress
        });

        await harness.MediaService.UpdateGameProgressAsync(new GameProgress
        {
            MediaItemId = item.Id,
            HoursPlayed = 18,
            Platform = "PC",
            CurrentStage = "Finale",
            CompletionState = CompletionState.Completed
        });

        var after = await harness.MediaService.GetByIdAsync(item.Id);
        int progressRowCount;

        await using (var db = await harness.DbFactory.CreateDbContextAsync())
        {
            progressRowCount = await db.GameProgresses.CountAsync(g => g.MediaItemId == item.Id);
        }

        Expect(after is not null, "Expected game item to exist.");
        Expect(progressRowCount == 1, "Expected game progress updates to upsert a single row.");
        Expect(after!.UpdatedAt > before, "Expected UpdatedAt to advance after saving game progress.");
        Expect(after.GameProgress?.CompletionState == CompletionState.Completed, "Expected latest completion state to be stored.");
        Expect(after.GameProgress?.CurrentStage == "Finale", "Expected latest current stage to be stored.");
    }

    private static async Task SearchViewModelReportsMissingProviderConfig()
    {
        var provider = new FakeProvider(isConfigured: false);
        bool imported = false;
        bool manual = false;
        bool cancelled = false;

        var vm = new SearchExternalViewModel(
            [provider],
            new FakeImageCacheService(),
            new FakeMediaService(),
            _ => imported = true,
            () => manual = true,
            () => cancelled = true)
        {
            SearchQuery = "Persona",
            SelectedType = MediaType.Game
        };

        await vm.SearchCommand.ExecuteAsync(null);

        Expect(vm.ErrorMessage == provider.ConfigurationHint, "Expected missing configuration message.");
        Expect(vm.Results.Count == 0, "Expected no search results when provider is not configured.");
        Expect(provider.SearchCalls == 0, "Expected provider search not to run without configuration.");
        Expect(!imported && !manual && !cancelled, "Expected no side-effect callbacks during validation failure.");
    }

    private static Task MediaInputValidatorRejectsEmptyTitle()
    {
        var error = MediaInputValidator.ValidateMedia(
            title: "   ",
            releaseYear: 2025,
            userScore: 8,
            totalEpisodes: 12,
            totalSeasons: 1,
            runtimeMinutes: 45);

        Expect(error == "Title is required.", "Expected an empty title to be rejected.");
        return Task.CompletedTask;
    }

    private static Task MediaInputValidatorRejectsInvalidMediaRanges()
    {
        var yearError = MediaInputValidator.ValidateMedia(
            title: "Valid",
            releaseYear: 1700,
            userScore: 8,
            totalEpisodes: 12,
            totalSeasons: 1,
            runtimeMinutes: 45);

        var ratingError = MediaInputValidator.ValidateMedia(
            title: "Valid",
            releaseYear: 2025,
            userScore: 11,
            totalEpisodes: 12,
            totalSeasons: 1,
            runtimeMinutes: 45);

        var runtimeError = MediaInputValidator.ValidateMedia(
            title: "Valid",
            releaseYear: 2025,
            userScore: 8,
            totalEpisodes: 12,
            totalSeasons: 1,
            runtimeMinutes: 0);

        Expect(yearError is not null && yearError.Contains("Release year"), "Expected an out-of-range year to be rejected.");
        Expect(ratingError == "Your rating must be between 1 and 10.", "Expected out-of-range rating to be rejected.");
        Expect(runtimeError == "Runtime must be greater than zero.", "Expected zero runtime to be rejected.");
        return Task.CompletedTask;
    }

    private static Task MediaInputValidatorRejectsNegativeGameHours()
    {
        var error = MediaInputValidator.ValidateGameProgress(-3);
        Expect(error == "Hours played cannot be negative.", "Expected negative game hours to be rejected.");
        return Task.CompletedTask;
    }

    private static async Task DetailViewModelClearsStaleStateWhenItemMissing()
    {
        await using var harness = await TestHarness.CreateAsync();
        var item = await harness.MediaService.CreateAsync(new MediaItem
        {
            Title = "Detail Reset Test",
            MediaType = MediaType.Series,
            TotalSeasons = 2
        });

        var vm = new DetailViewModel(
            harness.MediaService,
            Array.Empty<IMetadataProvider>(),
            () => { },
            _ => { },
            () => { });

        await vm.LoadCommand.ExecuteAsync(item.Id);

        Expect(vm.Item is not null, "Expected the detail view model to load the requested item.");
        Expect(vm.IsSeriesOrAnime, "Expected series content to set the series/anime flag.");
        Expect(vm.EpisodesViewModel is not null, "Expected the episodes view model to be created for series.");

        await vm.LoadCommand.ExecuteAsync(-999);

        Expect(vm.Item is null, "Expected the item to be cleared when a later load fails.");
        Expect(!vm.IsSeriesOrAnime, "Expected the series/anime flag to reset when loading fails.");
        Expect(!vm.IsGame, "Expected the game flag to reset when loading fails.");
        Expect(vm.EpisodesViewModel is null, "Expected the episodes view model to be cleared when loading fails.");
        Expect(vm.GameProgressViewModel is null, "Expected the game progress view model to be cleared when loading fails.");
        Expect(vm.ErrorMessage == "This item could not be found anymore.", "Expected a friendly not-found message.");
    }

    private static async Task EpisodesViewModelReportsMissingProvider()
    {
        await using var harness = await TestHarness.CreateAsync();
        var vm = new EpisodesViewModel(
            harness.MediaService,
            Array.Empty<IMetadataProvider>(),
            mediaItemId: 1,
            externalId: "abc123",
            providerName: "MissingProvider",
            totalSeasons: 1);

        await vm.FetchAllSeasonsCommand.ExecuteAsync(null);

        Expect(vm.ErrorMessage == "Episode data provider is not available for this item.", "Expected a friendly provider availability error.");
    }

    private static Task LibraryViewModelTogglesDisplayModes()
    {
        LibraryDisplayMode lastMode = LibraryDisplayMode.Grid;
        var vm = new LibraryViewModel(
            new FakeMediaService(),
            _ => { },
            () => { },
            initialDisplayMode: LibraryDisplayMode.List,
            onDisplayModeChanged: mode => lastMode = mode);

        Expect(vm.DisplayMode == LibraryDisplayMode.List, "Expected the initial display mode to be applied.");
        Expect(vm.IsListMode, "Expected list mode flag to be active.");
        Expect(!vm.IsGridMode, "Expected grid mode flag to be inactive when list mode is selected.");

        vm.SetDisplayModeCommand.Execute(LibraryDisplayMode.Grid);

        Expect(vm.DisplayMode == LibraryDisplayMode.Grid, "Expected the view model to switch to grid mode.");
        Expect(vm.IsGridMode, "Expected grid mode flag to be active after toggling.");
        Expect(!vm.IsListMode, "Expected list mode flag to be inactive after toggling.");
        Expect(lastMode == LibraryDisplayMode.Grid, "Expected the display mode callback to receive the updated mode.");
        return Task.CompletedTask;
    }

    private static async Task ResilientHttpServiceCachesSuccessfulJsonResponses()
    {
        int calls = 0;
        using var handler = new SequenceHttpMessageHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"value":"cached"}""", Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());

        var first = await service.GetJsonAsync<TestPayload>("https://example.test/payload", "payload-cache", TimeSpan.FromMinutes(5));
        var second = await service.GetJsonAsync<TestPayload>("https://example.test/payload", "payload-cache", TimeSpan.FromMinutes(5));

        Expect(first?.Value == "cached", "Expected the first payload to deserialize correctly.");
        Expect(second?.Value == "cached", "Expected the cached payload to be returned on the second call.");
        Expect(calls == 1, "Expected the second call to be served from cache.");
    }

    private static async Task ResilientHttpServiceRetriesTransientFailures()
    {
        int calls = 0;
        using var handler = new SequenceHttpMessageHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"value":"retried"}""", Encoding.UTF8, "application/json")
                };
        });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());

        var result = await service.GetJsonAsync<TestPayload>("https://example.test/retry", "payload-retry", TimeSpan.FromMinutes(1));

        Expect(result?.Value == "retried", "Expected the service to succeed after retrying a transient failure.");
        Expect(calls == 2, "Expected exactly one retry after the first transient failure.");
    }

    private static async Task ProvidersReflectUpdatedSettingsWithoutRestart()
    {
        int calls = 0;
        using var handler = new SequenceHttpMessageHandler(request =>
        {
            calls++;
            Expect(
                request.RequestUri?.Query.Contains("key=live-key", StringComparison.Ordinal) == true,
                "Expected the provider to use the updated API key from settings.");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"results":[{"id":99,"name":"Hades","slug":"hades","released":"2020-09-17","background_image":"https://example.test/hades.jpg","genres":[{"name":"Roguelike"}]}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new ResilientHttpService(client, cache, new FakeLogger<ResilientHttpService>());
        var settings = new AppSettings();
        var provider = new RawgProvider(http, settings, new FakeLogger<RawgProvider>());

        Expect(!provider.IsConfigured, "Expected the provider to start unconfigured without an API key.");

        settings.RawgApiKey = "live-key";

        Expect(provider.IsConfigured, "Expected the provider to reflect settings changes without restart.");

        var results = await provider.SearchAsync("Hades", MediaType.Game);

        Expect(calls == 1, "Expected the provider search to run after the API key was updated.");
        Expect(results.Count == 1 && results[0].Title == "Hades", "Expected the provider to return parsed search results after the settings update.");
    }

    private static async Task AppUpdateServiceDetectsNewerVersionFromLocalFeed()
    {
        string feedDirectory = Path.Combine(Path.GetTempPath(), $"media-tracker-update-feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(feedDirectory);

        try
        {
            string manifestPath = Path.Combine(feedDirectory, "MediaTracker.latest.json");
            await File.WriteAllTextAsync(
                manifestPath,
                """
                {
                  "version": "1.4.0",
                  "downloadUrl": "MediaTracker-setup-1.4.0.exe",
                  "notes": "Fresh release"
                }
                """);

            var service = CreateUpdateService();
            service.CurrentVersionProvider = () => "1.0.0";

            var result = await service.CheckForUpdatesAsync(manifestPath, bypassCache: true);

            Expect(result.Succeeded, "Expected the update check to succeed for a valid local feed.");
            Expect(result.IsUpdateAvailable, "Expected a newer feed version to be detected.");
            Expect(result.LatestVersion == "1.4.0", "Expected the latest version to come from the feed.");
            Expect(
                result.DownloadLocation == Path.Combine(feedDirectory, "MediaTracker-setup-1.4.0.exe"),
                "Expected a relative download path to resolve beside the feed.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(feedDirectory))
                    Directory.Delete(feedDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task AppUpdateServiceReportsUpToDate()
    {
        string feedDirectory = Path.Combine(Path.GetTempPath(), $"media-tracker-update-feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(feedDirectory);

        try
        {
            string manifestPath = Path.Combine(feedDirectory, "MediaTracker.latest.json");
            await File.WriteAllTextAsync(
                manifestPath,
                """
                {
                  "version": "2.0.0",
                  "downloadUrl": "MediaTracker-setup-2.0.0.exe"
                }
                """);

            var service = CreateUpdateService();
            service.CurrentVersionProvider = () => "2.0.0";

            var result = await service.CheckForUpdatesAsync(manifestPath, bypassCache: true);

            Expect(result.Succeeded, "Expected the update check to succeed for a matching version.");
            Expect(!result.IsUpdateAvailable, "Expected no update banner when the current version matches the feed.");
            Expect(result.Message.Contains("latest version", StringComparison.OrdinalIgnoreCase), "Expected a friendly up-to-date message.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(feedDirectory))
                    Directory.Delete(feedDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static AppUpdateService CreateUpdateService()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var resilientHttp = new ResilientHttpService(new HttpClient(), cache, new FakeLogger<ResilientHttpService>());
        return new AppUpdateService(resilientHttp, new FakeLogger<AppUpdateService>());
    }
}

internal sealed class TestHarness : IAsyncDisposable
{
    private readonly string _databasePath;
    public TestDbContextFactory DbFactory { get; }
    public MediaService MediaService { get; }

    private TestHarness(string databasePath, TestDbContextFactory dbFactory)
    {
        _databasePath = databasePath;
        DbFactory = dbFactory;
        MediaService = new MediaService(dbFactory);
    }

    public static async Task<TestHarness> CreateAsync()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"media-tracker-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var factory = new TestDbContextFactory(options);

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        return new TestHarness(databasePath, factory);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch
        {
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppDbContext(_options));
}

internal sealed class FakeProvider : IMetadataProvider
{
    private readonly bool _isConfigured;

    public FakeProvider(bool isConfigured)
    {
        _isConfigured = isConfigured;
    }

    public string Name => "Fake";
    public bool IsConfigured => _isConfigured;
    public string ConfigurationHint => "Add the fake provider key first.";
    public MediaType[] SupportedTypes => [MediaType.Game];
    public int SearchCalls { get; private set; }

    public Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
    {
        SearchCalls++;
        return Task.FromResult(new List<SearchResult>());
    }

    public Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default)
        => Task.FromResult<SearchResult?>(null);

    public Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default)
        => Task.FromResult(new List<EpisodeResult>());
}

internal sealed class FakeImageCacheService : ImageCacheService
{
    public FakeImageCacheService() : base(new FakeResilientHttpService(), new FakeLogger<ImageCacheService>())
    {
    }
}

internal sealed class FakeMediaService : MediaService
{
    public FakeMediaService() : base(new TestDbContextFactory(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options))
    {
    }
}

internal sealed class FakeResilientHttpService : ResilientHttpService
{
    public FakeResilientHttpService() : base(new HttpClient(), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), new FakeLogger<ResilientHttpService>())
    {
    }
}

internal sealed class FakeLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}

internal sealed class SequenceHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public SequenceHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_respond(request));
}

internal sealed class TestPayload
{
    public string? Value { get; set; }
}
