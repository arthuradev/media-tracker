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
    private readonly Action _onBack;
    private readonly Action<MediaItem> _onEdit;
    private readonly Action _onDeleted;

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
        Action onBack,
        Action<MediaItem> onEdit,
        Action onDeleted)
    {
        _mediaService = mediaService;
        _providers = providers;
        _onBack = onBack;
        _onEdit = onEdit;
        _onDeleted = onDeleted;
    }

    [RelayCommand]
    private async Task LoadAsync(int id)
    {
        IsLoading = true;
        ErrorMessage = null;
        Item = null;
        IsSeriesOrAnime = false;
        IsGame = false;
        EpisodesViewModel = null;
        GameProgressViewModel = null;
        try
        {
            Item = await _mediaService.GetByIdAsync(id);
            if (Item is null)
            {
                ErrorMessage = "This item could not be found anymore.";
                return;
            }

            IsSeriesOrAnime = Item.MediaType is MediaType.Series or MediaType.Anime;
            IsGame = Item.MediaType is MediaType.Game;

            if (IsSeriesOrAnime)
            {
                var mapping = Item.ProviderMappings.FirstOrDefault();
                EpisodesViewModel = new EpisodesViewModel(
                    _mediaService, _providers, Item.Id,
                    mapping?.ExternalId, mapping?.ProviderName,
                    Item.TotalSeasons);
            }

            if (IsGame)
            {
                GameProgressViewModel = new GameProgressViewModel(
                    _mediaService, Item.Id, Item.GameProgress);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "The details for this item could not be loaded right now.";
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
            ErrorMessage = "This item could not be deleted right now.";
        }
    }
}
