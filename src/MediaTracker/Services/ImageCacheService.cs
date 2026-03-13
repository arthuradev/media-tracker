using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MediaTracker.Helpers;

namespace MediaTracker.Services;

public class ImageCacheService
{
    private readonly ResilientHttpService _http;
    private readonly ILogger<ImageCacheService> _logger;

    public ImageCacheService(ResilientHttpService http, ILogger<ImageCacheService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> DownloadAndCacheAsync(string? imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl)))[..16];
            var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext))
                ext = ".jpg";

            var filePath = Path.Combine(AppPaths.ImageCacheDir, $"{hash}{ext}");
            if (File.Exists(filePath))
                return filePath;

            var bytes = await _http.DownloadBytesAsync(imageUrl, ct);
            if (bytes is null || bytes.Length == 0)
                return null;

            await File.WriteAllBytesAsync(filePath, bytes, ct);
            return filePath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Image download failed for {ImageUrl}", imageUrl);
            return null;
        }
    }
}
