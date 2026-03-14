using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaTracker.Helpers;
using MediaTracker.Models;

namespace MediaTracker.Services;

public class AppSettings
{
    private const int CurrentFormatVersion = 3;
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions WriteOptions = new(ReadOptions)
    {
        WriteIndented = true
    };

    public string TmdbApiKey { get; set; } = string.Empty;
    public string RawgApiKey { get; set; } = string.Empty;
    public string DeepLApiKey { get; set; } = string.Empty;
    public string UpdateFeedUrl { get; set; } = string.Empty;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public AppLanguage PreferredLanguage { get; set; } = AppLanguageCatalog.InferFromCulture(CultureInfo.CurrentUICulture);
    public bool HasUnreadableSecrets { get; private set; }
    public string? StoragePath { get; private set; }

    public static AppSettings Load(string? path = null, CultureInfo? currentCulture = null)
    {
        try
        {
            string settingsPath = ResolvePath(path);
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var persisted = JsonSerializer.Deserialize<PersistedAppSettings>(json, ReadOptions);
                if (persisted is not null)
                    return FromPersisted(persisted, settingsPath, currentCulture);
            }
        }
        catch { }

        return new AppSettings
        {
            PreferredLanguage = AppLanguageCatalog.InferFromCulture(currentCulture ?? CultureInfo.CurrentUICulture),
            StoragePath = ResolvePath(path)
        };
    }

    public void Save(string? path = null)
    {
        string settingsPath = ResolvePath(path ?? StoragePath);
        string directory = Path.GetDirectoryName(settingsPath) ?? AppPaths.AppDataDir;

        Directory.CreateDirectory(directory);

        var persisted = new PersistedAppSettings
        {
            Version = CurrentFormatVersion,
            TmdbApiKeyProtected = ProtectSecret(TmdbApiKey),
            RawgApiKeyProtected = ProtectSecret(RawgApiKey),
            DeepLApiKeyProtected = ProtectSecret(DeepLApiKey),
            UpdateFeedUrl = UpdateFeedUrl,
            CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
            PreferredLanguage = AppLanguageCatalog.GetCultureCode(PreferredLanguage)
        };

        var json = JsonSerializer.Serialize(persisted, WriteOptions);
        File.WriteAllText(settingsPath, json);
        HasUnreadableSecrets = false;
        StoragePath = settingsPath;
    }

    private static AppSettings FromPersisted(PersistedAppSettings persisted, string settingsPath, CultureInfo? currentCulture)
    {
        bool hasUnreadableSecrets = false;
        AppLanguage preferredLanguage = ResolvePreferredLanguage(persisted.PreferredLanguage, currentCulture);

        var settings = new AppSettings
        {
            TmdbApiKey = ReadSecret(persisted.TmdbApiKeyProtected, persisted.TmdbApiKey, ref hasUnreadableSecrets),
            RawgApiKey = ReadSecret(persisted.RawgApiKeyProtected, persisted.RawgApiKey, ref hasUnreadableSecrets),
            DeepLApiKey = ReadSecret(persisted.DeepLApiKeyProtected, persisted.DeepLApiKey, ref hasUnreadableSecrets),
            UpdateFeedUrl = persisted.UpdateFeedUrl ?? string.Empty,
            CheckForUpdatesOnStartup = persisted.CheckForUpdatesOnStartup,
            PreferredLanguage = preferredLanguage,
            HasUnreadableSecrets = hasUnreadableSecrets,
            StoragePath = settingsPath
        };

        return settings;
    }

    private static string ResolvePath(string? path)
    {
        string resolved = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(AppPaths.AppDataDir, "settings.json")
            : path;

        return Path.GetFullPath(resolved);
    }

    private static string ReadSecret(string? protectedValue, string? legacyValue, ref bool hasUnreadableSecrets)
    {
        if (!string.IsNullOrWhiteSpace(protectedValue))
        {
            if (TryUnprotectSecret(protectedValue, out string secret))
                return secret;

            hasUnreadableSecrets = true;
            return string.Empty;
        }

        return legacyValue?.Trim() ?? string.Empty;
    }

    private static string? ProtectSecret(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
            return null;

        byte[] secretBytes = Encoding.UTF8.GetBytes(normalized);
        byte[] protectedBytes = ProtectedData.Protect(secretBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static bool TryUnprotectSecret(string protectedValue, out string value)
    {
        try
        {
            byte[] protectedBytes = Convert.FromBase64String(protectedValue);
            byte[] secretBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            value = Encoding.UTF8.GetString(secretBytes);
            return true;
        }
        catch (ArgumentException) { }
        catch (CryptographicException) { }
        catch (FormatException) { }

        value = string.Empty;
        return false;
    }

    private static AppLanguage ResolvePreferredLanguage(string? persistedValue, CultureInfo? currentCulture)
    {
        if (AppLanguageCatalog.TryParse(persistedValue, out AppLanguage language))
            return language;

        return AppLanguageCatalog.InferFromCulture(currentCulture ?? CultureInfo.CurrentUICulture);
    }

    private sealed class PersistedAppSettings
    {
        public int Version { get; set; } = CurrentFormatVersion;
        public string? TmdbApiKey { get; set; }
        public string? RawgApiKey { get; set; }
        public string? DeepLApiKey { get; set; }
        public string? TmdbApiKeyProtected { get; set; }
        public string? RawgApiKeyProtected { get; set; }
        public string? DeepLApiKeyProtected { get; set; }
        public string UpdateFeedUrl { get; set; } = string.Empty;
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public string? PreferredLanguage { get; set; }
    }
}
