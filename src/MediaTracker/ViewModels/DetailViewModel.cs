using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;

namespace MediaTracker.ViewModels;

public partial class DetailViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly IEnumerable<IMetadataProvider> _providers;
    private readonly LocalizationService _localization;
    private readonly Action _onBack;
    private readonly Action<MediaItem> _onEdit;
    private readonly Action _onDeleted;
    private int? _loadedItemId;
    private int _loadVersion;

    [ObservableProperty]
    private MediaItem? _item;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSeriesOrAnime;

    [ObservableProperty]
    private bool _isGame;

    [ObservableProperty]
    private EpisodesViewModel? _episodesViewModel;

    [ObservableProperty]
    private GameProgressViewModel? _gameProgressViewModel;

    [ObservableProperty]
    private string? _errorMessage;

    public DetailViewModel(
        MediaService mediaService,
        IEnumerable<IMetadataProvider> providers,
        LocalizationService localization,
        Action onBack,
        Action<MediaItem> onEdit,
        Action onDeleted)
    {
        _mediaService = mediaService;
        _providers = providers;
        _localization = localization;
        _onBack = onBack;
        _onEdit = onEdit;
        _onDeleted = onDeleted;

        PropertyChangedEventManager.AddHandler(_localization, OnLocalizationPropertyChanged, nameof(LocalizationService.CurrentLanguage));
    }

    [RelayCommand]
    private async Task LoadAsync(int id)
    {
        _loadedItemId = id;
        int loadVersion = Interlocked.Increment(ref _loadVersion);

        IsLoading = true;
        ResetViewState();

        try
        {
            var item = await _mediaService.GetByIdAsync(id);
            if (loadVersion != _loadVersion)
                return;

            if (item is null)
            {
                ErrorMessage = _localization.Get("detail.loadMissing");
                return;
            }

            item = await RefreshLocalizedContentAsync(item, loadVersion);
            if (loadVersion != _loadVersion)
                return;

            ApplyItemState(item);
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("detail.loadError");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GoBack() => _onBack();

    [RelayCommand]
    private void Edit()
    {
        if (Item is not null)
            _onEdit(Item);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Item is null) return;
        try
        {
            await _mediaService.DeleteAsync(Item.Id);
            _onDeleted();
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("detail.deleteError");
        }
    }

    private void ResetViewState()
    {
        ErrorMessage = null;
        Item = null;
        IsSeriesOrAnime = false;
        IsGame = false;
        EpisodesViewModel = null;
        GameProgressViewModel = null;
    }

    private void ApplyItemState(MediaItem item)
    {
        Item = item;
        IsSeriesOrAnime = item.MediaType is MediaType.Series or MediaType.Anime;
        IsGame = item.MediaType is MediaType.Game;

        var mapping = item.ProviderMappings.FirstOrDefault();

        EpisodesViewModel = IsSeriesOrAnime
            ? new EpisodesViewModel(
                _mediaService,
                _providers,
                _localization,
                item.Id,
                mapping?.ExternalId,
                mapping?.ProviderName,
                item.TotalSeasons)
            : null;

        GameProgressViewModel = IsGame
            ? new GameProgressViewModel(_mediaService, _localization, item.Id, item.GameProgress)
            : null;
    }

    private async Task<MediaItem> RefreshLocalizedContentAsync(MediaItem item, int loadVersion)
    {
        var mapping = item.ProviderMappings.FirstOrDefault();
        if (mapping is null)
            return item;

        var provider = _providers.FirstOrDefault(p => p.Name == mapping.ProviderName && p.IsConfigured);
        if (provider is null)
            return item;

        bool metadataChanged = false;

        try
        {
            var details = await provider.GetDetailsAsync(mapping.ExternalId, item.MediaType);
            if (loadVersion != _loadVersion)
                return item;

            if (details is not null)
            {
                await _mediaService.UpdateProviderMetadataAsync(item.Id, details);
                metadataChanged = true;
            }

            if (item.MediaType is MediaType.Series or MediaType.Anime)
            {
                metadataChanged |= await RefreshEpisodeContentAsync(item, mapping, provider, loadVersion) > 0;
            }
        }
        catch
        {
            return item;
        }

        if (!metadataChanged)
            return item;

        return await _mediaService.GetByIdAsync(item.Id) ?? item;
    }

    private async Task<int> RefreshEpisodeContentAsync(
        MediaItem item,
        ProviderMapping mapping,
        IMetadataProvider provider,
        int loadVersion)
    {
        var seasonNumbers = item.Episodes
            .Select(e => e.SeasonNumber)
            .Distinct()
            .OrderBy(number => number)
            .ToList();

        if (seasonNumbers.Count == 0)
            return 0;

        var localizedEpisodes = new List<Episode>();

        foreach (int seasonNumber in seasonNumbers)
        {
            var fetchedEpisodes = await provider.GetEpisodesAsync(mapping.ExternalId, seasonNumber);
            if (loadVersion != _loadVersion)
                return 0;

            localizedEpisodes.AddRange(fetchedEpisodes.Select(episode => new Episode
            {
                MediaItemId = item.Id,
                SeasonNumber = episode.SeasonNumber,
                EpisodeNumber = episode.EpisodeNumber,
                Title = episode.Title,
                Overview = episode.Overview,
                AirDate = episode.AirDate,
                Runtime = episode.Runtime
            }));
        }

        return await _mediaService.UpsertEpisodesAsync(localizedEpisodes);
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loadedItemId is not int id || id <= 0 || Item is null)
            return;

        _ = RefreshCurrentItemAsync(id);
    }

    private async Task RefreshCurrentItemAsync(int id)
    {
        int loadVersion = Interlocked.Increment(ref _loadVersion);

        try
        {
            var item = await _mediaService.GetByIdAsync(id);
            if (loadVersion != _loadVersion || item is null)
                return;

            item = await RefreshLocalizedContentAsync(item, loadVersion);
            if (loadVersion != _loadVersion)
                return;

            ApplyItemState(item);
        }
        catch
        {
        }
    }
}
