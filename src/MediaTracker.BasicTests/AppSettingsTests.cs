using MediaTracker.Services;

namespace MediaTracker.BasicTests;

public sealed class AppSettingsTests
{
    [Fact]
    public void SaveProtectsSecretsOnDiskAndLoadRoundTrips()
    {
        string settingsPath = CreateSettingsPath();

        try
        {
            var settings = new AppSettings
            {
                TmdbApiKey = "tmdb-secret",
                RawgApiKey = "rawg-secret",
                UpdateFeedUrl = "https://example.test/feed.json",
                CheckForUpdatesOnStartup = false
            };

            settings.Save(settingsPath);

            string json = File.ReadAllText(settingsPath);
            Assert.DoesNotContain("tmdb-secret", json);
            Assert.DoesNotContain("rawg-secret", json);
            Assert.Contains("TmdbApiKeyProtected", json, StringComparison.Ordinal);
            Assert.Contains("RawgApiKeyProtected", json, StringComparison.Ordinal);

            var loaded = AppSettings.Load(settingsPath);

            Assert.Equal("tmdb-secret", loaded.TmdbApiKey);
            Assert.Equal("rawg-secret", loaded.RawgApiKey);
            Assert.Equal("https://example.test/feed.json", loaded.UpdateFeedUrl);
            Assert.False(loaded.CheckForUpdatesOnStartup);
            Assert.False(loaded.HasUnreadableSecrets);
        }
        finally
        {
            DeleteSettingsFile(settingsPath);
        }
    }

    [Fact]
    public void LoadReadsLegacyPlaintextSettingsAndSaveMigratesThem()
    {
        string settingsPath = CreateSettingsPath();

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "tmdbApiKey": "legacy-tmdb",
                  "rawgApiKey": "legacy-rawg",
                  "updateFeedUrl": "https://example.test/legacy-feed.json",
                  "checkForUpdatesOnStartup": true
                }
                """);

            var loaded = AppSettings.Load(settingsPath);

            Assert.Equal("legacy-tmdb", loaded.TmdbApiKey);
            Assert.Equal("legacy-rawg", loaded.RawgApiKey);
            Assert.Equal("https://example.test/legacy-feed.json", loaded.UpdateFeedUrl);
            Assert.True(loaded.CheckForUpdatesOnStartup);
            Assert.False(loaded.HasUnreadableSecrets);

            loaded.Save(settingsPath);

            string migratedJson = File.ReadAllText(settingsPath);
            Assert.DoesNotContain("legacy-tmdb", migratedJson);
            Assert.DoesNotContain("legacy-rawg", migratedJson);
            Assert.Contains("TmdbApiKeyProtected", migratedJson, StringComparison.Ordinal);
            Assert.Contains("RawgApiKeyProtected", migratedJson, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSettingsFile(settingsPath);
        }
    }

    [Fact]
    public void LoadTreatsUnreadableProtectedSecretsAsMissing()
    {
        string settingsPath = CreateSettingsPath();

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "version": 2,
                  "tmdbApiKeyProtected": "not-base64",
                  "rawgApiKeyProtected": "also-not-base64",
                  "updateFeedUrl": "https://example.test/feed.json",
                  "checkForUpdatesOnStartup": false
                }
                """);

            var loaded = AppSettings.Load(settingsPath);

            Assert.Equal(string.Empty, loaded.TmdbApiKey);
            Assert.Equal(string.Empty, loaded.RawgApiKey);
            Assert.Equal("https://example.test/feed.json", loaded.UpdateFeedUrl);
            Assert.False(loaded.CheckForUpdatesOnStartup);
            Assert.True(loaded.HasUnreadableSecrets);
        }
        finally
        {
            DeleteSettingsFile(settingsPath);
        }
    }

    private static string CreateSettingsPath()
        => Path.Combine(Path.GetTempPath(), $"media-tracker-settings-{Guid.NewGuid():N}.json");

    private static void DeleteSettingsFile(string settingsPath)
    {
        try
        {
            if (File.Exists(settingsPath))
                File.Delete(settingsPath);
        }
        catch
        {
        }
    }
}
