using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Helpers;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private readonly AppUpdateService _appUpdateService;
    private readonly Action<AppUpdateCheckResult>? _onUpdateCheckCompleted;

    [ObservableProperty]
    private SettingsTab _selectedTab = SettingsTab.General;

    [ObservableProperty]
    private string _tmdbApiKey;

    [ObservableProperty]
    private string _rawgApiKey;

    [ObservableProperty]
    private string _updateFeedUrl;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyLanguageCommand))]
    private AppLanguage _selectedLanguage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _languageStatusMessage;

    [ObservableProperty]
    private string? _languageErrorMessage;

    [ObservableProperty]
    private string? _updateStatusMessage;

    [ObservableProperty]
    private string? _updateErrorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string? _latestDownloadLocation;

    public ObservableCollection<LocalizedOption<AppLanguage>> LanguageOptions { get; }

    public string CurrentVersion { get; }

    public string CurrentLanguageDisplayName => _localization.GetLanguageDisplayName(_localization.CurrentLanguage);

    public SettingsViewModel(
        AppSettings settings,
        LocalizationService localization,
        AppUpdateService appUpdateService,
        Action<AppUpdateCheckResult>? onUpdateCheckCompleted = null)
    {
        _settings = settings;
        _localization = localization;
        _appUpdateService = appUpdateService;
        _onUpdateCheckCompleted = onUpdateCheckCompleted;

        _tmdbApiKey = settings.TmdbApiKey;
        _rawgApiKey = settings.RawgApiKey;
        _updateFeedUrl = settings.UpdateFeedUrl;
        _checkForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
        _selectedLanguage = settings.PreferredLanguage;
        CurrentVersion = appUpdateService.CurrentVersion;
        LanguageOptions = new ObservableCollection<LocalizedOption<AppLanguage>>(
            AppLanguageCatalog.SupportedLanguages.Select(language => new LocalizedOption<AppLanguage>
            {
                Value = language,
                Label = _localization.GetLanguageDisplayName(language)
            }));

        PropertyChangedEventManager.AddHandler(_localization, OnLocalizationPropertyChanged, nameof(LocalizationService.CurrentLanguage));

        if (settings.HasUnreadableSecrets)
            ErrorMessage = _localization.Get("settings.unreadableSecrets");
    }

    [RelayCommand]
    private void SelectTab(SettingsTab tab)
    {
        SelectedTab = tab;
    }

    [RelayCommand(CanExecute = nameof(CanApplyLanguage))]
    private void ApplyLanguage()
    {
        LanguageStatusMessage = null;
        LanguageErrorMessage = null;

        try
        {
            _localization.SetLanguage(SelectedLanguage);
            _settings.Save();
            LanguageStatusMessage = _localization.Get("settings.general.applied");
            OnPropertyChanged(nameof(CurrentLanguageDisplayName));
            ApplyLanguageCommand.NotifyCanExecuteChanged();
        }
        catch (Exception)
        {
            LanguageErrorMessage = _localization.Get("settings.saveError");
        }
    }

    [RelayCommand]
    private void Save()
    {
        StatusMessage = null;
        ErrorMessage = null;

        try
        {
            _settings.TmdbApiKey = TmdbApiKey.Trim();
            _settings.RawgApiKey = RawgApiKey.Trim();
            _settings.UpdateFeedUrl = UpdateFeedUrl.Trim();
            _settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
            _settings.Save();
            StatusMessage = _localization.Get("settings.saved");
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("settings.saveError");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        UpdateStatusMessage = null;
        UpdateErrorMessage = null;
        IsCheckingForUpdates = true;

        try
        {
            string feedUrl = UpdateFeedUrl.Trim();
            if (!string.Equals(feedUrl, UpdateFeedUrl, StringComparison.Ordinal))
                UpdateFeedUrl = feedUrl;

            var result = await _appUpdateService.CheckForUpdatesAsync(feedUrl, bypassCache: true);
            LatestDownloadLocation = result.DownloadLocation;

            if (result.Succeeded)
            {
                UpdateStatusMessage = result.Message;
                _onUpdateCheckCompleted?.Invoke(result);
            }
            else
            {
                UpdateErrorMessage = result.Message;
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenLatestDownload))]
    private void OpenLatestDownload()
    {
        if (string.IsNullOrWhiteSpace(LatestDownloadLocation))
            return;

        if (ShellLauncher.TryOpen(LatestDownloadLocation))
            return;

        UpdateErrorMessage = _localization.Get("settings.downloadOpenError");
    }

    partial void OnLatestDownloadLocationChanged(string? value)
    {
        OpenLatestDownloadCommand.NotifyCanExecuteChanged();
    }

    private bool CanApplyLanguage()
    {
        return SelectedLanguage != _localization.CurrentLanguage;
    }

    private bool CanCheckForUpdates()
    {
        return !IsCheckingForUpdates;
    }

    private bool CanOpenLatestDownload()
    {
        return !string.IsNullOrWhiteSpace(LatestDownloadLocation);
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentLanguageDisplayName));

        if (_settings.HasUnreadableSecrets)
            ErrorMessage = _localization.Get("settings.unreadableSecrets");
    }
}
