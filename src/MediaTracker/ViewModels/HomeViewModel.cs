using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly Action _onAddMedia;
    private readonly Action _onOpenLibrary;
    private readonly Action _onOpenSettings;
    private readonly MediaService _mediaService;

    [ObservableProperty]
    private int _movieCount;

    [ObservableProperty]
    private int _seriesCount;

    [ObservableProperty]
    private int _animeCount;

    [ObservableProperty]
    private int _gameCount;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _isEmpty = true;

    public HomeViewModel(
        MediaService mediaService,
        Action onAddMedia,
        Action onOpenLibrary,
        Action onOpenSettings)
    {
        _mediaService = mediaService;
        _onAddMedia = onAddMedia;
        _onOpenLibrary = onOpenLibrary;
        _onOpenSettings = onOpenSettings;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var items = await _mediaService.GetAllAsync();

        MovieCount = items.Count(i => i.MediaType == MediaType.Movie);
        SeriesCount = items.Count(i => i.MediaType == MediaType.Series);
        AnimeCount = items.Count(i => i.MediaType == MediaType.Anime);
        GameCount = items.Count(i => i.MediaType == MediaType.Game);

        HasItems = items.Count > 0;
        IsEmpty = !HasItems;
    }

    [RelayCommand]
    private void AddMedia() => _onAddMedia();

    [RelayCommand]
    private void OpenLibrary() => _onOpenLibrary();

    [RelayCommand]
    private void OpenSettings() => _onOpenSettings();
}
