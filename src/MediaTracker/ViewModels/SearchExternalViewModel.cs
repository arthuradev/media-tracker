using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;

namespace MediaTracker.ViewModels;

public partial class SearchExternalViewModel : ObservableObject
{
    private readonly IEnumerable<IMetadataProvider> _providers;
    private readonly ImageCacheService _imageCache;
    private readonly MediaService _mediaService;
    private readonly Action<MediaItem> _onImported;
    private readonly Action _onManualAdd;
    private readonly Action _onCancelled;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private MediaType _selectedType = MediaType.Series;

    [ObservableProperty]
    private ObservableCollection<SearchResult> _results = [];

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _showEmptyState;

    public Array MediaTypes => Enum.GetValues<MediaType>();

    public SearchExternalViewModel(
        IEnumerable<IMetadataProvider> providers,
        ImageCacheService imageCache,
        MediaService mediaService,
        Action<MediaItem> onImported,
        Action onManualAdd,
        Action onCancelled)
    {
        _providers = providers;
        _imageCache = imageCache;
        _mediaService = mediaService;
        _onImported = onImported;
        _onManualAdd = onManualAdd;
        _onCancelled = onCancelled;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching = true;
        ErrorMessage = null;
        HasSearched = true;
        ShowEmptyState = false;

        try
        {
            var provider = _providers.FirstOrDefault(p => p.SupportedTypes.Contains(SelectedType));
            if (provider is null)
            {
                ErrorMessage = "No provider available for this type.";
                Results = [];
                UpdateEmptyState();
                return;
            }

            if (!provider.IsConfigured)
            {
                ErrorMessage = provider.ConfigurationHint;
                Results = [];
                UpdateEmptyState();
                return;
            }

            var results = await provider.SearchAsync(SearchQuery, SelectedType, ct);
            if (!ct.IsCancellationRequested)
                Results = new ObservableCollection<SearchResult>(results);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            ErrorMessage = "Search could not be completed right now.";
            Results = [];
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsSearching = false;
                UpdateEmptyState();
            }
        }
    }

    [RelayCommand]
    private async Task ImportAsync(SearchResult result)
    {
        IsImporting = true;
        ErrorMessage = null;

        try
        {
            // Get full details
            var provider = _providers.FirstOrDefault(p => p.Name == result.ProviderName);
            var details = provider is not null
                ? await provider.GetDetailsAsync(result.ExternalId, result.MediaType) ?? result
                : result;

            // Cache images
            var coverPath = await _imageCache.DownloadAndCacheAsync(details.CoverImageUrl);
            var backdropPath = await _imageCache.DownloadAndCacheAsync(details.BackdropImageUrl);

            // Create media item
            var item = new MediaItem
            {
                Title = details.Title,
                OriginalTitle = details.OriginalTitle,
                MediaType = details.MediaType,
                ReleaseYear = details.ReleaseYear,
                Synopsis = details.Synopsis,
                CoverImagePath = coverPath,
                BackdropImagePath = backdropPath,
                Genres = details.Genres,
                TotalEpisodes = details.TotalEpisodes,
                TotalSeasons = details.TotalSeasons,
                RuntimeMinutes = details.RuntimeMinutes,
                Status = MediaStatus.PlanToWatch,
                LastSyncedAt = DateTime.UtcNow
            };

            var saved = await _mediaService.CreateAsync(item);

            // Save provider mapping
            await _mediaService.AddProviderMappingAsync(saved.Id, new ProviderMapping
            {
                MediaItemId = saved.Id,
                ProviderName = details.ProviderName,
                ExternalId = details.ExternalId,
                ExternalUrl = details.ExternalUrl
            });

            _onImported(saved);
        }
        catch (Exception)
        {
            ErrorMessage = "This item could not be imported right now.";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _onCancelled();

    [RelayCommand]
    private void ManualAdd() => _onManualAdd();

    private void UpdateEmptyState()
    {
        ShowEmptyState = HasSearched &&
                         !IsSearching &&
                         string.IsNullOrEmpty(ErrorMessage) &&
                         Results.Count == 0;
    }
}
