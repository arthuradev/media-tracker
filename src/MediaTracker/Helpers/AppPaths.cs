using System.IO;

namespace MediaTracker.Helpers;

public static class AppPaths
{
    private static readonly string _appData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MediaTracker");

    public static string AppDataDir => _appData;
    public static string DatabasePath => Path.Combine(_appData, "catalog.db");
    public static string ImageCacheDir => Path.Combine(_appData, "cache", "images");
    public static string LogDir => Path.Combine(_appData, "logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(_appData);
        Directory.CreateDirectory(ImageCacheDir);
        Directory.CreateDirectory(LogDir);
    }
}
