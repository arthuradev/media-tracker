using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly Action<int> _onOpenDetail;
    private readonly Action _onAddNew;
    private readonly Action<LibraryDisplayMode>? _onDisplayModeChanged;
    private CancellationTokenSource? _searchDebounceCts;
    private int _loadVersion;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _items = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _showLoadingSkeleton;

    [ObservableProperty]
    private MediaType? _typeFilter;

    [ObservableProperty]
    private MediaStatus? _statusFilter;

    [ObservableProperty]
    private LibraryDisplayMode _displayMode = LibraryDisplayMode.Grid;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _title = "Library";

    [ObservableProperty]
    private string _emptyTitle = "No media yet";

    [ObservableProperty]
    private string _emptyMessage = "Add something new to start building your collection.";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isGridMode = true;

    [ObservableProperty]
    private bool _isListMode;

    public LibraryViewModel(
        MediaService mediaService,
        Action<int> onOpenDetail,
        Action onAddNew,
        LibraryDisplayMode initialDisplayMode = LibraryDisplayMode.Grid,
        Action<LibraryDisplayMode>? onDisplayModeChanged = null)
    {
        _mediaService = mediaService;
        _onOpenDetail = onOpenDetail;
        _onAddNew = onAddNew;
        _onDisplayModeChanged = onDisplayModeChanged;
        DisplayMode = initialDisplayMode;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        int loadVersion = Interlocked.Increment(ref _loadVersion);
        IsLoading = true;
        ShowLoadingSkeleton = Items.Count == 0;
        ErrorMessage = null;

        try
        {
            var items = await _mediaService.GetAllAsync(TypeFilter, StatusFilter, SearchQuery);

            if (loadVersion != _loadVersion)
                return;

            Items = new ObservableCollection<MediaCardViewModel>(
                items.Select(MediaCardViewModel.FromModel));
            IsEmpty = Items.Count == 0;
            UpdateEmptyState();
        }
        catch (Exception)
        {
            if (loadVersion != _loadVersion)
                return;

            ErrorMessage = "The library could not be loaded right now.";
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                IsLoading = false;
                ShowLoadingSkeleton = false;
            }
        }
    }

    [RelayCommand]
    private void OpenDetail(int id) => _onOpenDetail(id);

    [RelayCommand]
    private void AddNew() => _onAddNew();

    [RelayCommand]
    private async Task SetTypeFilter(MediaType? type)
    {
        TypeFilter = type;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SetStatusFilter(MediaStatus? status)
    {
        StatusFilter = status;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private void SetDisplayMode(LibraryDisplayMode mode)
    {
        if (DisplayMode == mode)
            return;

        DisplayMode = mode;
        _onDisplayModeChanged?.Invoke(mode);
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = DebounceSearchAsync();
    }

    partial void OnDisplayModeChanged(LibraryDisplayMode value)
    {
        IsGridMode = value == LibraryDisplayMode.Grid;
        IsListMode = value == LibraryDisplayMode.List;
    }

    private void UpdateEmptyState()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery) || StatusFilter is not null)
        {
            EmptyTitle = "No matches found";
            EmptyMessage = "Try a broader search or clear one of the status filters.";
            return;
        }

        EmptyTitle = Title switch
        {
            "Series" => "No series yet",
            "Anime" => "No anime yet",
            "Movies" => "No movies yet",
            "Games" => "No games yet",
            _ => "No media yet"
        };

        EmptyMessage = "Add something new to start building your collection.";
    }

    private async Task DebounceSearchAsync()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var ct = _searchDebounceCts.Token;

        try
        {
            await Task.Delay(220, ct);
            if (!ct.IsCancellationRequested)
                await LoadAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
