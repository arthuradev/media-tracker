using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;

namespace MediaTracker.ViewModels;

public partial class EpisodesViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly IEnumerable<IMetadataProvider> _providers;
    private readonly LocalizationService _localization;
    private readonly int _mediaItemId;
    private readonly string? _externalId;
    private readonly string? _providerName;
    private readonly int? _totalSeasons;

    [ObservableProperty]
    private ObservableCollection<SeasonGroup> _seasons = [];

    [ObservableProperty]
    private bool _canFetch;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasEpisodes;

    [ObservableProperty]
    private string _progressSummary = string.Empty;

    [ObservableProperty]
    private bool _showLoadingSkeleton;

    [ObservableProperty]
    private string _emptyStateMessage = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public EpisodesViewModel(
        MediaService mediaService,
        IEnumerable<IMetadataProvider> providers,
        LocalizationService localization,
        int mediaItemId,
        string? externalId,
        string? providerName,
        int? totalSeasons = null)
    {
        _mediaService = mediaService;
        _providers = providers;
        _localization = localization;
        _mediaItemId = mediaItemId;
        _externalId = externalId;
        _providerName = providerName;
        _totalSeasons = totalSeasons;
        CanFetch = !string.IsNullOrEmpty(externalId) && !string.IsNullOrEmpty(providerName);
        EmptyStateMessage = _localization.Get("episodes.empty.unavailable");
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ShowLoadingSkeleton = Seasons.Count == 0;
        ErrorMessage = null;
        try
        {
            var episodes = await _mediaService.GetEpisodesAsync(_mediaItemId);

            var groups = episodes
                .GroupBy(e => e.SeasonNumber)
                .OrderBy(g => g.Key)
                .Select(g => new SeasonGroup
                {
                    SeasonNumber = g.Key,
                    Episodes = new ObservableCollection<EpisodeViewModel>(
                        g.Select(EpisodeViewModel.FromModel))
                })
                .ToList();

            Seasons = new ObservableCollection<SeasonGroup>(groups);
            HasEpisodes = Seasons.Count > 0;
            UpdateProgressSummary();
            EmptyStateMessage = CanFetch
                ? _localization.Get("episodes.empty.fetchable")
                : _localization.Get("episodes.empty.unavailable");
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("episodes.loadError");
        }
        finally
        {
            IsLoading = false;
            ShowLoadingSkeleton = false;
        }
    }

    [RelayCommand]
    private async Task ToggleEpisodeAsync(EpisodeViewModel ep)
    {
        ErrorMessage = null;

        try
        {
            await _mediaService.ToggleEpisodeWatchedAsync(ep.Id);
            ep.IsWatched = !ep.IsWatched;

            var season = Seasons.FirstOrDefault(s => s.SeasonNumber == ep.SeasonNumber);
            season?.Refresh();
            UpdateProgressSummary();
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("episodes.updateError");
        }
    }

    [RelayCommand]
    private async Task MarkSeasonWatchedAsync(SeasonGroup season)
    {
        ErrorMessage = null;

        try
        {
            bool markAs = !season.AllWatched;
            await _mediaService.MarkSeasonWatchedAsync(_mediaItemId, season.SeasonNumber, markAs);

            foreach (var ep in season.Episodes)
                ep.IsWatched = markAs;

            season.Refresh();
            UpdateProgressSummary();
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("episodes.seasonError");
        }
    }

    [RelayCommand]
    private async Task FetchEpisodesAsync(int seasonNumber)
    {
        var provider = GetProvider();
        if (provider is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await FetchSeasonAsync(provider, seasonNumber);
            await LoadAsync();
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("episodes.fetchError");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task FetchAllSeasonsAsync()
    {
        if (!CanFetch || _totalSeasons is null or 0) return;

        var provider = GetProvider();
        if (provider is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            bool changed = false;
            for (int s = 1; s <= _totalSeasons; s++)
            {
                changed |= await FetchSeasonAsync(provider, s) > 0;
            }

            if (changed || Seasons.Count == 0)
                await LoadAsync();
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("episodes.fetchError");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateProgressSummary()
    {
        var total = Seasons.Sum(s => s.TotalCount);
        var watched = Seasons.Sum(s => s.WatchedCount);
        ProgressSummary = total > 0 ? _localization.Format("progress.episodesSummary", watched, total) : string.Empty;
    }

    private IMetadataProvider? GetProvider()
    {
        if (string.IsNullOrEmpty(_externalId) || string.IsNullOrEmpty(_providerName))
            return null;

        var provider = _providers.FirstOrDefault(p => p.Name == _providerName);
        if (provider is null)
        {
            ErrorMessage = _localization.Get("episodes.providerMissing");
            return null;
        }

        if (!provider.IsConfigured)
        {
            ErrorMessage = provider.ConfigurationHint;
            return null;
        }

        return provider;
    }

    private async Task<int> FetchSeasonAsync(IMetadataProvider provider, int seasonNumber)
    {
        if (string.IsNullOrEmpty(_externalId))
            return 0;

        var fetched = await provider.GetEpisodesAsync(_externalId, seasonNumber);
        if (fetched.Count == 0)
            return 0;

        var newEpisodes = fetched.Select(ep => new Episode
        {
            MediaItemId = _mediaItemId,
            SeasonNumber = ep.SeasonNumber,
            EpisodeNumber = ep.EpisodeNumber,
            Title = ep.Title,
            Overview = ep.Overview,
            AirDate = ep.AirDate,
            Runtime = ep.Runtime
        }).ToList();

        return await _mediaService.AddEpisodesAsync(newEpisodes);
    }
}
