using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaTracker.Helpers;

namespace MediaTracker.Services;

public class AppSettings
{
    private const int CurrentFormatVersion = 2;
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
    public string UpdateFeedUrl { get; set; } = string.Empty;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool HasUnreadableSecrets { get; private set; }

    public static AppSettings Load(string? path = null)
    {
        try
        {
            string settingsPath = ResolvePath(path);
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var persisted = JsonSerializer.Deserialize<PersistedAppSettings>(json, ReadOptions);
                if (persisted is not null)
                    return FromPersisted(persisted);
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save(string? path = null)
    {
        string settingsPath = ResolvePath(path);
        string directory = Path.GetDirectoryName(settingsPath) ?? AppPaths.AppDataDir;

        Directory.CreateDirectory(directory);

        var persisted = new PersistedAppSettings
        {
            Version = CurrentFormatVersion,
            TmdbApiKeyProtected = ProtectSecret(TmdbApiKey),
            RawgApiKeyProtected = ProtectSecret(RawgApiKey),
            UpdateFeedUrl = UpdateFeedUrl,
            CheckForUpdatesOnStartup = CheckForUpdatesOnStartup
        };

        var json = JsonSerializer.Serialize(persisted, WriteOptions);
        File.WriteAllText(settingsPath, json);
        HasUnreadableSecrets = false;
    }

    private static AppSettings FromPersisted(PersistedAppSettings persisted)
    {
        bool hasUnreadableSecrets = false;

        var settings = new AppSettings
        {
            TmdbApiKey = ReadSecret(persisted.TmdbApiKeyProtected, persisted.TmdbApiKey, ref hasUnreadableSecrets),
            RawgApiKey = ReadSecret(persisted.RawgApiKeyProtected, persisted.RawgApiKey, ref hasUnreadableSecrets),
            UpdateFeedUrl = persisted.UpdateFeedUrl ?? string.Empty,
            CheckForUpdatesOnStartup = persisted.CheckForUpdatesOnStartup,
            HasUnreadableSecrets = hasUnreadableSecrets
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

    private sealed class PersistedAppSettings
    {
        public int Version { get; set; } = CurrentFormatVersion;
        public string? TmdbApiKey { get; set; }
        public string? RawgApiKey { get; set; }
        public string? TmdbApiKeyProtected { get; set; }
        public string? RawgApiKeyProtected { get; set; }
        public string UpdateFeedUrl { get; set; } = string.Empty;
        public bool CheckForUpdatesOnStartup { get; set; } = true;
    }
}
