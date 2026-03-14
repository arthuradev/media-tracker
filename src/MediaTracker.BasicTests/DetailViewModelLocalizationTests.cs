using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;
using MediaTracker.ViewModels;

namespace MediaTracker.BasicTests;

public sealed class DetailViewModelLocalizationTests
{
    [Fact]
    public async Task DetailViewModelRefreshesProviderContentWhenLanguageChanges()
    {
        await using var harness = await TestHarness.CreateAsync();
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);

        var item = await harness.MediaService.CreateAsync(new MediaItem
        {
            Title = "Localized Series",
            MediaType = MediaType.Series,
            Synopsis = "Seed synopsis",
            Genres = "Seed genre",
            TotalSeasons = 1,
            TotalEpisodes = 1
        });

        await harness.MediaService.AddProviderMappingAsync(item.Id, new ProviderMapping
        {
            MediaItemId = item.Id,
            ProviderName = LocalizedDetailProvider.ProviderNameValue,
            ExternalId = "series-1"
        });

        await harness.MediaService.AddEpisodesAsync(
        [
            new Episode
            {
                MediaItemId = item.Id,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Pilot",
                Overview = "English overview"
            }
        ]);

        var vm = new DetailViewModel(
            harness.MediaService,
            [new LocalizedDetailProvider(localization)],
            localization,
            () => { },
            _ => { },
            () => { });

        await vm.LoadCommand.ExecuteAsync(item.Id);

        Assert.Equal("English synopsis", vm.Item?.Synopsis);
        Assert.Equal("Action", vm.Item?.Genres);

        localization.SetLanguage(AppLanguage.PortugueseBrazil);

        await WaitForConditionAsync(() => vm.Item?.Synopsis == "Sinopse em português");

        var reloaded = await harness.MediaService.GetByIdAsync(item.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Sinopse em português", reloaded!.Synopsis);
        Assert.Equal("Ação", reloaded.Genres);
        Assert.Equal("Piloto PT", reloaded.Episodes.Single().Title);
        Assert.NotNull(reloaded.LastSyncedAt);
    }

    [Fact]
    public async Task DetailViewModelRefreshesGameContentWhenLanguageChanges()
    {
        await using var harness = await TestHarness.CreateAsync();
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);

        var item = await harness.MediaService.CreateAsync(new MediaItem
        {
            Title = "Localized Game",
            MediaType = MediaType.Game,
            Synopsis = "Seed game synopsis",
            Genres = "Action"
        });

        await harness.MediaService.AddProviderMappingAsync(item.Id, new ProviderMapping
        {
            MediaItemId = item.Id,
            ProviderName = LocalizedGameProvider.ProviderNameValue,
            ExternalId = "game-1"
        });

        var vm = new DetailViewModel(
            harness.MediaService,
            [new LocalizedGameProvider(localization)],
            localization,
            () => { },
            _ => { },
            () => { });

        await vm.LoadCommand.ExecuteAsync(item.Id);

        Assert.Equal("English game synopsis", vm.Item?.Synopsis);

        localization.SetLanguage(AppLanguage.PortugueseBrazil);

        await WaitForConditionAsync(() => vm.Item?.Synopsis == "Sinopse do jogo em português");

        var reloaded = await harness.MediaService.GetByIdAsync(item.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Sinopse do jogo em português", reloaded!.Synopsis);
        Assert.Equal("Ação", reloaded.Genres);
        Assert.NotNull(reloaded.LastSyncedAt);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (condition())
                return;

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private sealed class LocalizedDetailProvider : IMetadataProvider
    {
        private readonly LocalizationService _localization;

        public const string ProviderNameValue = "LocalizedFake";

        public LocalizedDetailProvider(LocalizationService localization)
        {
            _localization = localization;
        }

        public string Name => ProviderNameValue;
        public bool IsConfigured => true;
        public string ConfigurationHint => string.Empty;
        public MediaType[] SupportedTypes => [MediaType.Series];

        public Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
            => Task.FromResult(new List<SearchResult>());

        public Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default)
        {
            var details = _localization.CurrentLanguage switch
            {
                AppLanguage.PortugueseBrazil => new SearchResult
                {
                    ExternalId = externalId,
                    Title = "Série localizada",
                    OriginalTitle = "Localized Series",
                    MediaType = MediaType.Series,
                    Synopsis = "Sinopse em português",
                    Genres = "Ação",
                    TotalSeasons = 1,
                    TotalEpisodes = 1,
                    ProviderName = Name
                },
                _ => new SearchResult
                {
                    ExternalId = externalId,
                    Title = "Localized Series",
                    OriginalTitle = "Localized Series",
                    MediaType = MediaType.Series,
                    Synopsis = "English synopsis",
                    Genres = "Action",
                    TotalSeasons = 1,
                    TotalEpisodes = 1,
                    ProviderName = Name
                }
            };

            return Task.FromResult<SearchResult?>(details);
        }

        public Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default)
        {
            string title = _localization.CurrentLanguage == AppLanguage.PortugueseBrazil ? "Piloto PT" : "Pilot";
            string overview = _localization.CurrentLanguage == AppLanguage.PortugueseBrazil ? "Resumo PT" : "English overview";

            return Task.FromResult(new List<EpisodeResult>
            {
                new()
                {
                    SeasonNumber = seasonNumber,
                    EpisodeNumber = 1,
                    Title = title,
                    Overview = overview
                }
            });
        }
    }

    private sealed class LocalizedGameProvider : IMetadataProvider
    {
        private readonly LocalizationService _localization;

        public const string ProviderNameValue = "LocalizedGame";

        public LocalizedGameProvider(LocalizationService localization)
        {
            _localization = localization;
        }

        public string Name => ProviderNameValue;
        public bool IsConfigured => true;
        public string ConfigurationHint => string.Empty;
        public MediaType[] SupportedTypes => [MediaType.Game];

        public Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
            => Task.FromResult(new List<SearchResult>());

        public Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default)
        {
            var details = _localization.CurrentLanguage switch
            {
                AppLanguage.PortugueseBrazil => new SearchResult
                {
                    ExternalId = externalId,
                    Title = "Jogo localizado",
                    MediaType = MediaType.Game,
                    Synopsis = "Sinopse do jogo em português",
                    Genres = "Ação",
                    ProviderName = Name
                },
                _ => new SearchResult
                {
                    ExternalId = externalId,
                    Title = "Localized Game",
                    MediaType = MediaType.Game,
                    Synopsis = "English game synopsis",
                    Genres = "Action",
                    ProviderName = Name
                }
            };

            return Task.FromResult<SearchResult?>(details);
        }

        public Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default)
            => Task.FromResult(new List<EpisodeResult>());
    }
}
