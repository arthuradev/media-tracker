using System.Reflection;

namespace MediaTracker.Helpers;

public static class AppVersionHelper
{
    public static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Trim();

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public static bool IsNewerVersion(string? currentVersion, string? latestVersion)
    {
        if (!TryParseComparableVersion(currentVersion, out var current))
            return false;

        if (!TryParseComparableVersion(latestVersion, out var latest))
            return false;

        return latest > current;
    }

    public static bool TryParseComparableVersion(string? versionText, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(versionText))
            return false;

        string normalized = versionText.Trim();

        int prereleaseIndex = normalized.IndexOfAny(['-', '+']);
        if (prereleaseIndex >= 0)
            normalized = normalized[..prereleaseIndex];

        if (!Version.TryParse(normalized, out var parsedVersion))
            return false;

        version = parsedVersion;
        return true;
    }
}
