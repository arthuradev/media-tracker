using System.IO;
using System.Text.Json;
using MediaTracker.Helpers;
using Microsoft.Extensions.Logging;

namespace MediaTracker.Services;

public sealed class AppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ResilientHttpService _httpService;
    private readonly LocalizationService _localization;
    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(
        ResilientHttpService httpService,
        LocalizationService localization,
        ILogger<AppUpdateService> logger)
    {
        _httpService = httpService;
        _localization = localization;
        _logger = logger;
    }

    public Func<string> CurrentVersionProvider { get; set; } = AppVersionHelper.GetCurrentVersion;

    public string CurrentVersion => CurrentVersionProvider();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(
        string? feedLocation,
        bool bypassCache = false,
        CancellationToken ct = default)
    {
        string currentVersion = CurrentVersion;

        if (string.IsNullOrWhiteSpace(feedLocation))
        {
            return AppUpdateCheckResult.Failure(
                currentVersion,
                _localization.Get("update.feedRequired"));
        }

        string normalizedFeedLocation = feedLocation.Trim();

        try
        {
            AppUpdateManifest? manifest = await LoadManifestAsync(normalizedFeedLocation, bypassCache, ct);

            if (manifest is null)
            {
                return AppUpdateCheckResult.Failure(
                    currentVersion,
                    _localization.Get("update.feedUnavailable"));
            }

            if (!AppVersionHelper.TryParseComparableVersion(manifest.Version, out _))
            {
                return AppUpdateCheckResult.Failure(
                    currentVersion,
                    _localization.Get("update.invalidVersion"));
            }

            string? downloadLocation = ResolveDownloadLocation(normalizedFeedLocation, manifest.DownloadUrl);
            string latestVersion = manifest.Version.Trim();

            if (AppVersionHelper.IsNewerVersion(currentVersion, latestVersion))
            {
                string message = string.IsNullOrWhiteSpace(downloadLocation)
                    ? _localization.Format("update.availableWithoutDownload", latestVersion)
                    : _localization.Format("update.availableWithDownload", latestVersion, currentVersion);

                return AppUpdateCheckResult.UpdateAvailable(
                    currentVersion,
                    latestVersion,
                    downloadLocation,
                    manifest.Notes,
                    message);
            }

            return AppUpdateCheckResult.UpToDate(
                currentVersion,
                latestVersion,
                _localization.Format("update.upToDate", currentVersion));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed for feed {FeedLocation}", normalizedFeedLocation);

            return AppUpdateCheckResult.Failure(
                currentVersion,
                _localization.Get("update.checkFailed"));
        }
    }

    private async Task<AppUpdateManifest?> LoadManifestAsync(
        string feedLocation,
        bool bypassCache,
        CancellationToken ct)
    {
        if (Uri.TryCreate(feedLocation, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
            {
                return await _httpService.GetJsonAsync<AppUpdateManifest>(
                    absoluteUri.ToString(),
                    $"update-feed::{absoluteUri}",
                    TimeSpan.FromMinutes(10),
                    ct,
                    bypassCache);
            }

            if (absoluteUri.IsFile)
            {
                return await LoadManifestFromFileAsync(absoluteUri.LocalPath, ct);
            }
        }

        if (!Path.IsPathRooted(feedLocation))
        {
            throw new InvalidOperationException("Update feeds must use an absolute URL or file path.");
        }

        return await LoadManifestFromFileAsync(feedLocation, ct);
    }

    private static async Task<AppUpdateManifest?> LoadManifestFromFileAsync(string path, CancellationToken ct)
    {
        string normalizedPath = Path.GetFullPath(path);

        if (!File.Exists(normalizedPath))
            return null;

        await using var stream = File.OpenRead(normalizedPath);
        return await JsonSerializer.DeserializeAsync<AppUpdateManifest>(stream, JsonOptions, ct);
    }

    private static string? ResolveDownloadLocation(string feedLocation, string? downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return null;

        string normalizedDownloadUrl = downloadUrl.Trim();

        if (Uri.TryCreate(normalizedDownloadUrl, UriKind.Absolute, out var absoluteDownloadUri))
        {
            return absoluteDownloadUri.IsFile
                ? absoluteDownloadUri.LocalPath
                : absoluteDownloadUri.ToString();
        }

        if (Uri.TryCreate(feedLocation, UriKind.Absolute, out var feedUri))
        {
            if (feedUri.Scheme == Uri.UriSchemeHttp || feedUri.Scheme == Uri.UriSchemeHttps)
                return new Uri(feedUri, normalizedDownloadUrl).ToString();

            if (feedUri.IsFile)
            {
                string baseDirectory = Path.GetDirectoryName(feedUri.LocalPath) ?? string.Empty;
                return Path.GetFullPath(Path.Combine(baseDirectory, normalizedDownloadUrl));
            }
        }

        if (Path.IsPathRooted(feedLocation))
        {
            string baseDirectory = Path.GetDirectoryName(feedLocation) ?? string.Empty;
            return Path.GetFullPath(Path.Combine(baseDirectory, normalizedDownloadUrl));
        }

        return normalizedDownloadUrl;
    }
}

public sealed class AppUpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? PortableUrl { get; set; }
    public string? Notes { get; set; }
    public string? PublishedAtUtc { get; set; }
}

public sealed class AppUpdateCheckResult
{
    public bool Succeeded { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? LatestVersion { get; init; }
    public string? DownloadLocation { get; init; }
    public string? Notes { get; init; }
    public string Message { get; init; } = string.Empty;

    public static AppUpdateCheckResult Failure(string currentVersion, string message) => new()
    {
        Succeeded = false,
        CurrentVersion = currentVersion,
        Message = message
    };

    public static AppUpdateCheckResult UpToDate(string currentVersion, string latestVersion, string message) => new()
    {
        Succeeded = true,
        CurrentVersion = currentVersion,
        LatestVersion = latestVersion,
        Message = message
    };

    public static AppUpdateCheckResult UpdateAvailable(
        string currentVersion,
        string latestVersion,
        string? downloadLocation,
        string? notes,
        string message) => new()
    {
        Succeeded = true,
        IsUpdateAvailable = true,
        CurrentVersion = currentVersion,
        LatestVersion = latestVersion,
        DownloadLocation = downloadLocation,
        Notes = notes,
        Message = message
    };
}
