using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;
using MediaTracker.ViewModels;

namespace MediaTracker.BasicTests;

public sealed class RobustnessTests
{
    [Fact]
    public async Task AddEpisodesAsync_SkipsDuplicates()
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

        Assert.Equal(2, firstInsert);
        Assert.Equal(1, secondInsert);
        Assert.Equal(3, episodes.Count);
    }

    [Fact]
    public async Task ProgressUpdatesTouchParentTimestamp()
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

        Assert.NotNull(after);
        Assert.True(after!.UpdatedAt > before);
        Assert.True(after.Episodes.Single().IsWatched);
    }

    [Fact]
    public async Task GameProgressUpsertsAndTouchesParent()
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

        Assert.NotNull(after);
        Assert.Equal(1, progressRowCount);
        Assert.True(after!.UpdatedAt > before);
        Assert.Equal(CompletionState.Completed, after.GameProgress?.CompletionState);
        Assert.Equal("Finale", after.GameProgress?.CurrentStage);
    }

    [Fact]
    public void MediaInputValidatorRejectsEmptyTitle()
    {
        var error = MediaInputValidator.ValidateMedia(
            title: "   ",
            releaseYear: 2025,
            userScore: 8,
            totalEpisodes: 12,
            totalSeasons: 1,
            runtimeMinutes: 45);

        Assert.Equal("Title is required.", error);
    }

    [Fact]
    public void MediaInputValidatorRejectsInvalidMediaRanges()
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

        Assert.Contains("Release year", yearError);
        Assert.Equal("Your rating must be between 1 and 10.", ratingError);
        Assert.Equal("Runtime must be greater than zero.", runtimeError);
    }

    [Fact]
    public void MediaInputValidatorRejectsNegativeGameHours()
    {
        var error = MediaInputValidator.ValidateGameProgress(-3);
        Assert.Equal("Hours played cannot be negative.", error);
    }

    [Fact]
    public async Task DetailViewModelClearsStaleStateWhenItemMissing()
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

        Assert.NotNull(vm.Item);
        Assert.True(vm.IsSeriesOrAnime);
        Assert.NotNull(vm.EpisodesViewModel);

        await vm.LoadCommand.ExecuteAsync(-999);

        Assert.Null(vm.Item);
        Assert.False(vm.IsSeriesOrAnime);
        Assert.False(vm.IsGame);
        Assert.Null(vm.EpisodesViewModel);
        Assert.Null(vm.GameProgressViewModel);
        Assert.Equal("This item could not be found anymore.", vm.ErrorMessage);
    }

    [Fact]
    public async Task EpisodesViewModelReportsMissingProvider()
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

        Assert.Equal("Episode data provider is not available for this item.", vm.ErrorMessage);
    }

    [Fact]
    public void LibraryViewModelTogglesDisplayModes()
    {
        LibraryDisplayMode lastMode = LibraryDisplayMode.Grid;
        var vm = new LibraryViewModel(
            new FakeMediaService(),
            _ => { },
            () => { },
            initialDisplayMode: LibraryDisplayMode.List,
            onDisplayModeChanged: mode => lastMode = mode);

        Assert.Equal(LibraryDisplayMode.List, vm.DisplayMode);
        Assert.True(vm.IsListMode);
        Assert.False(vm.IsGridMode);

        vm.SetDisplayModeCommand.Execute(LibraryDisplayMode.Grid);

        Assert.Equal(LibraryDisplayMode.Grid, vm.DisplayMode);
        Assert.True(vm.IsGridMode);
        Assert.False(vm.IsListMode);
        Assert.Equal(LibraryDisplayMode.Grid, lastMode);
    }

    [Fact]
    public async Task ResilientHttpServiceCachesSuccessfulJsonResponses()
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

        Assert.Equal("cached", first?.Value);
        Assert.Equal("cached", second?.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResilientHttpServiceRetriesTransientFailures()
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

        Assert.Equal("retried", result?.Value);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ProvidersReflectUpdatedSettingsWithoutRestart()
    {
        int calls = 0;
        using var handler = new SequenceHttpMessageHandler(request =>
        {
            calls++;
            Assert.Contains("key=live-key", request.RequestUri?.Query);

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

        Assert.False(provider.IsConfigured);

        settings.RawgApiKey = "live-key";

        Assert.True(provider.IsConfigured);

        var results = await provider.SearchAsync("Hades", MediaType.Game);

        Assert.Equal(1, calls);
        Assert.Single(results);
        Assert.Equal("Hades", results[0].Title);
    }

    [Fact]
    public async Task AppUpdateServiceDetectsNewerVersionFromLocalFeed()
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

            var service = TestServices.CreateUpdateService();
            service.CurrentVersionProvider = () => "1.0.0";

            var result = await service.CheckForUpdatesAsync(manifestPath, bypassCache: true);

            Assert.True(result.Succeeded);
            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("1.4.0", result.LatestVersion);
            Assert.Equal(Path.Combine(feedDirectory, "MediaTracker-setup-1.4.0.exe"), result.DownloadLocation);
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

    [Fact]
    public async Task AppUpdateServiceReportsUpToDate()
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

            var service = TestServices.CreateUpdateService();
            service.CurrentVersionProvider = () => "2.0.0";

            var result = await service.CheckForUpdatesAsync(manifestPath, bypassCache: true);

            Assert.True(result.Succeeded);
            Assert.False(result.IsUpdateAvailable);
            Assert.Contains("latest version", result.Message, StringComparison.OrdinalIgnoreCase);
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
}
