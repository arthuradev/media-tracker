using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Helpers;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;
using MediaTracker.Views;

namespace MediaTracker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly IEnumerable<IMetadataProvider> _providers;
    private readonly ImageCacheService _imageCache;
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private readonly AppUpdateService _appUpdateService;
    private readonly Action? _openManualAddOverride;

    private CancellationTokenSource? _inlineSearchCts;
    private readonly DispatcherTimer _debounceTimer;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private AppSection _currentSection = AppSection.Home;

    [ObservableProperty]
    private string _currentSectionTitle = string.Empty;

    [ObservableProperty]
    private string _currentSectionSubtitle = string.Empty;

    partial void OnCurrentSectionChanged(AppSection value)
    {
        UpdateShellState(value);
    }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _canSearch;

    [ObservableProperty]
    private bool _canAddNew = true;

    [ObservableProperty]
    private LibraryDisplayMode _libraryDisplayMode = LibraryDisplayMode.Grid;

    [ObservableProperty]
    private bool _isUpdateBannerVisible;

    [ObservableProperty]
    private string _updateBannerMessage = string.Empty;

    [ObservableProperty]
    private string? _updateDownloadLocation;

    // ── Inline search state ──────────────────────────────────

    [ObservableProperty]
    private bool _isInlineSearchOpen;

    [ObservableProperty]
    private string _inlineSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SearchResult> _inlineResults = [];

    [ObservableProperty]
    private SearchResult? _selectedInlineResult;

    [ObservableProperty]
    private bool _isInlineSearching;

    [ObservableProperty]
    private bool _isInlineImporting;

    [ObservableProperty]
    private bool _showInlineResults;

    [ObservableProperty]
    private bool _showInlineEmpty;

    [ObservableProperty]
    private string? _inlineError;

    public bool HasProviders => _providers.Any(p => p.IsConfigured);

    public MainViewModel(
        MediaService mediaService,
        IEnumerable<IMetadataProvider> providers,
        ImageCacheService imageCache,
        AppSettings settings,
        LocalizationService localization,
        AppUpdateService appUpdateService,
        Action? openManualAddOverride = null,
        bool initializeShell = true)
    {
        _mediaService = mediaService;
        _providers = providers;
        _imageCache = imageCache;
        _settings = settings;
        _localization = localization;
        _appUpdateService = appUpdateService;
        _openManualAddOverride = openManualAddOverride;

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = RunInlineSearchAsync();
        };

        PropertyChangedEventManager.AddHandler(_localization, OnLocalizationPropertyChanged, nameof(LocalizationService.CurrentLanguage));

        if (initializeShell)
            NavigateTo(AppSection.Home);
        else
            UpdateShellState(CurrentSection);

        if (_settings.CheckForUpdatesOnStartup && !string.IsNullOrWhiteSpace(_settings.UpdateFeedUrl))
            _ = CheckForUpdatesOnStartupAsync();
    }

    // ── Inline search commands ───────────────────────────────

    [RelayCommand]
    private void ToggleInlineSearch()
    {
        if (!HasProviders)
        {
            OpenManualAddDialog();
            return;
        }

        IsInlineSearchOpen = !IsInlineSearchOpen;

        if (!IsInlineSearchOpen)
            ClearInlineSearch();
    }

    [RelayCommand]
    private void CloseInlineSearch()
    {
        IsInlineSearchOpen = false;
        ClearInlineSearch();
    }

    [RelayCommand]
    private async Task InlineImportAsync(SearchResult result)
    {
        if (IsInlineImporting) return;

        IsInlineImporting = true;
        InlineError = null;

        try
        {
            var provider = _providers.FirstOrDefault(p => p.Name == result.ProviderName);
            var details = provider is not null
                ? await provider.GetDetailsAsync(result.ExternalId, result.MediaType) ?? result
                : result;

            var coverPath = await _imageCache.DownloadAndCacheAsync(details.CoverImageUrl);
            var backdropPath = await _imageCache.DownloadAndCacheAsync(details.BackdropImageUrl);

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

            await _mediaService.AddProviderMappingAsync(saved.Id, new ProviderMapping
            {
                MediaItemId = saved.Id,
                ProviderName = details.ProviderName,
                ExternalId = details.ExternalId,
                ExternalUrl = details.ExternalUrl
            });

            CloseInlineSearch();
            NavigateTo(CurrentSection);
        }
        catch
        {
            InlineError = _localization.Get("shell.importFailed");
        }
        finally
        {
            IsInlineImporting = false;
        }
    }

    [RelayCommand]
    private void InlineManualAdd()
    {
        CloseInlineSearch();
        OpenManualAddDialog();
    }

    partial void OnInlineSearchQueryChanged(string value)
    {
        _debounceTimer.Stop();

        if (string.IsNullOrWhiteSpace(value))
        {
            _inlineSearchCts?.Cancel();
            InlineResults = [];
            SelectedInlineResult = null;
            ShowInlineResults = false;
            ShowInlineEmpty = false;
            InlineError = null;
            return;
        }

        _debounceTimer.Start();
    }

    private async Task RunInlineSearchAsync()
    {
        var query = InlineSearchQuery;
        if (string.IsNullOrWhiteSpace(query)) return;

        _inlineSearchCts?.Cancel();
        _inlineSearchCts = new CancellationTokenSource();
        var ct = _inlineSearchCts.Token;

        IsInlineSearching = true;
        ShowInlineResults = true;
        ShowInlineEmpty = false;
        InlineError = null;

        try
        {
            var configuredProviders = _providers.Where(p => p.IsConfigured).ToList();
            var tasks = new List<Task<List<SearchResult>>>();

            foreach (var provider in configuredProviders)
            {
                foreach (var mediaType in provider.SupportedTypes)
                {
                    tasks.Add(provider.SearchAsync(query, mediaType, ct));
                }
            }

            var results = await Task.WhenAll(tasks);

            if (ct.IsCancellationRequested) return;

            var allResults = new List<SearchResult>();
            foreach (var batch in results)
                allResults.AddRange(batch);

            var limited = allResults
                .GroupBy(r => r.Title.ToLowerInvariant())
                .Select(g => g.First())
                .Take(8)
                .ToList();

            InlineResults = new ObservableCollection<SearchResult>(limited);
            SelectedInlineResult = limited.FirstOrDefault();
            ShowInlineEmpty = limited.Count == 0;
        }
        catch (OperationCanceledException) { }
        catch
        {
            InlineError = _localization.Get("shell.searchFailed");
            InlineResults = [];
            SelectedInlineResult = null;
            ShowInlineEmpty = false;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsInlineSearching = false;
        }
    }

    private void ClearInlineSearch()
    {
        _debounceTimer.Stop();
        _inlineSearchCts?.Cancel();
        InlineSearchQuery = string.Empty;
        InlineResults = [];
        SelectedInlineResult = null;
        IsInlineSearching = false;
        ShowInlineResults = false;
        ShowInlineEmpty = false;
        InlineError = null;
    }

    public void MoveInlineSelection(int offset)
    {
        if (InlineResults.Count == 0)
        {
            SelectedInlineResult = null;
            return;
        }

        var currentIndex = SelectedInlineResult is null
            ? -1
            : InlineResults.IndexOf(SelectedInlineResult);

        int nextIndex = currentIndex switch
        {
            < 0 when offset >= 0 => 0,
            < 0 => InlineResults.Count - 1,
            _ => Math.Clamp(currentIndex + offset, 0, InlineResults.Count - 1)
        };

        SelectedInlineResult = InlineResults[nextIndex];
    }

    public bool TryImportSelectedInlineResult()
    {
        if (SelectedInlineResult is null || IsInlineImporting)
            return false;

        InlineImportCommand.Execute(SelectedInlineResult);
        return true;
    }

    // ── Navigation ───────────────────────────────────────────

    [RelayCommand]
    private void NavigateTo(AppSection section)
    {
        CurrentSection = section;
        UpdateShellState(section);

        if (section == AppSection.Home)
        {
            var homeVm = new HomeViewModel(
                _mediaService,
                onAddMedia: () => ToggleInlineSearch(),
                onOpenLibrary: () => NavigateTo(AppSection.Library),
                onOpenSettings: () => NavigateTo(AppSection.Settings));

            CurrentView = new HomeView { DataContext = homeVm };
            _ = homeVm.LoadCommand.ExecuteAsync(null);
            return;
        }

        if (section == AppSection.Favorites)
        {
            var placeholderVm = new PlaceholderViewModel(
                eyebrow: _localization.Get("favorites.eyebrow"),
                title: _localization.Get("favorites.title"),
                description: _localization.Get("favorites.description"),
                primaryActionText: _localization.Get("common.openLibrary"),
                secondaryActionText: _localization.Get("common.addSomethingNew"),
                onPrimaryAction: () => NavigateTo(AppSection.Library),
                onSecondaryAction: () => ToggleInlineSearch());

            CurrentView = new PlaceholderView { DataContext = placeholderVm };
            return;
        }

        if (section == AppSection.Settings)
        {
            var settingsVm = new SettingsViewModel(_settings, _localization, _appUpdateService, ApplyUpdateCheckResult);
            CurrentView = new SettingsView { DataContext = settingsVm };
            return;
        }

        MediaType? typeFilter = section switch
        {
            AppSection.Series => MediaType.Series,
            AppSection.Anime => MediaType.Anime,
            AppSection.Movies => MediaType.Movie,
            AppSection.Games => MediaType.Game,
            _ => null
        };

        var libraryVm = new LibraryViewModel(
            _mediaService,
            OpenDetail,
            () => ToggleInlineSearch(),
            _localization,
            initialDisplayMode: LibraryDisplayMode,
            onDisplayModeChanged: mode => LibraryDisplayMode = mode)
        {
            TypeFilter = typeFilter
        };

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            libraryVm.SearchQuery = SearchQuery;

        CurrentView = new LibraryView { DataContext = libraryVm };
    }

    [RelayCommand]
    private void AddNew() => ToggleInlineSearch();

    [RelayCommand]
    private void DismissUpdateBanner()
    {
        IsUpdateBannerVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanOpenUpdateDownload))]
    private void OpenUpdateDownload()
    {
        if (string.IsNullOrWhiteSpace(UpdateDownloadLocation))
            return;

        if (ShellLauncher.TryOpen(UpdateDownloadLocation))
            return;

        MessageBox.Show(
            _localization.Get("shell.updateOpenError"),
            _localization.Get("app.title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenDetail(int id)
    {
        var detailVm = new DetailViewModel(
            _mediaService,
            _providers,
            _localization,
            onBack: () => NavigateTo(CurrentSection),
            onEdit: item => OpenEditDialog(item),
            onDeleted: () => NavigateTo(CurrentSection));

        CurrentView = new DetailView { DataContext = detailVm };
        _ = detailVm.LoadCommand.ExecuteAsync(id);
    }

    private void OpenAddDialog() => ToggleInlineSearch();

    private void OpenManualAddDialog()
    {
        if (_openManualAddOverride is not null)
        {
            _openManualAddOverride();
            return;
        }

        var window = new AddEditMediaWindow { Owner = Application.Current.MainWindow };

        var vm = new AddEditMediaViewModel(
            _mediaService,
            _localization,
            onSaved: () => { window.DialogResult = true; window.Close(); },
            onCancelled: () => { window.DialogResult = false; window.Close(); });

        window.DataContext = vm;

        if (window.ShowDialog() == true)
            NavigateTo(CurrentSection);
    }

    private void OpenEditDialog(MediaItem item)
    {
        var window = new AddEditMediaWindow { Owner = Application.Current.MainWindow };

        var vm = new AddEditMediaViewModel(
            _mediaService,
            _localization,
            onSaved: () => { window.DialogResult = true; window.Close(); },
            onCancelled: () => { window.DialogResult = false; window.Close(); });

        vm.LoadForEdit(item);
        window.DataContext = vm;

        if (window.ShowDialog() == true)
            NavigateTo(CurrentSection);
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (CurrentView is LibraryView view && view.DataContext is LibraryViewModel libraryVm)
        {
            libraryVm.SearchQuery = value;
        }
    }

    partial void OnUpdateDownloadLocationChanged(string? value)
    {
        OpenUpdateDownloadCommand.NotifyCanExecuteChanged();
    }

    private void UpdateShellState(AppSection section)
    {
        CanSearch = section is AppSection.Library or AppSection.Series or AppSection.Anime or AppSection.Movies or AppSection.Games;
        CanAddNew = section != AppSection.Settings;
        CurrentSectionTitle = _localization.GetSectionLabel(section);
        CurrentSectionSubtitle = _localization.GetSectionSubtitle(section);
    }

    private bool CanOpenUpdateDownload()
    {
        return !string.IsNullOrWhiteSpace(UpdateDownloadLocation);
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        var result = await _appUpdateService.CheckForUpdatesAsync(_settings.UpdateFeedUrl);

        if (result.Succeeded)
            ApplyUpdateCheckResult(result);
    }

    private void ApplyUpdateCheckResult(AppUpdateCheckResult result)
    {
        if (result.IsUpdateAvailable)
        {
            UpdateBannerMessage = result.Message;
            UpdateDownloadLocation = result.DownloadLocation;
            IsUpdateBannerVisible = true;
            return;
        }

        if (result.Succeeded)
        {
            IsUpdateBannerVisible = false;
            UpdateBannerMessage = string.Empty;
            UpdateDownloadLocation = null;
        }
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateShellState(CurrentSection);
    }
}
