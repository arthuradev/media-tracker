using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Helpers;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly AppUpdateService _appUpdateService;
    private readonly Action<AppUpdateCheckResult>? _onUpdateCheckCompleted;

    [ObservableProperty]
    private string _tmdbApiKey;

    [ObservableProperty]
    private string _rawgApiKey;

    [ObservableProperty]
    private string _updateFeedUrl;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _updateStatusMessage;

    [ObservableProperty]
    private string? _updateErrorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string? _latestDownloadLocation;

    public string CurrentVersion { get; }

    public SettingsViewModel(
        AppSettings settings,
        AppUpdateService appUpdateService,
        Action<AppUpdateCheckResult>? onUpdateCheckCompleted = null)
    {
        _settings = settings;
        _appUpdateService = appUpdateService;
        _onUpdateCheckCompleted = onUpdateCheckCompleted;
        _tmdbApiKey = settings.TmdbApiKey;
        _rawgApiKey = settings.RawgApiKey;
        _updateFeedUrl = settings.UpdateFeedUrl;
        _checkForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
        CurrentVersion = appUpdateService.CurrentVersion;

        if (settings.HasUnreadableSecrets)
            ErrorMessage = "Some saved API keys could not be read. Add them again and save to protect them.";
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
            StatusMessage = "Settings saved.";
        }
        catch (Exception)
        {
            ErrorMessage = "Could not save settings right now.";
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

        UpdateErrorMessage = "The update download could not be opened right now.";
    }

    partial void OnLatestDownloadLocationChanged(string? value)
    {
        OpenLatestDownloadCommand.NotifyCanExecuteChanged();
    }

    private bool CanCheckForUpdates()
    {
        return !IsCheckingForUpdates;
    }

    private bool CanOpenLatestDownload()
    {
        return !string.IsNullOrWhiteSpace(LatestDownloadLocation);
    }
}
