using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MediaTracker.Services;

public class ResilientHttpService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ResilientHttpService> _logger;

    public ResilientHttpService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<ResilientHttpService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetJsonAsync<T>(
        string url,
        string cacheKey,
        TimeSpan cacheDuration,
        CancellationToken ct = default,
        bool bypassCache = false)
    {
        if (!bypassCache && _cache.TryGetValue(cacheKey, out T? cached))
            return cached;

        using var response = await SendWithRetryAsync(url, ct);
        if (response is null)
            return default;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);

        if (value is not null)
            _cache.Set(cacheKey, value, cacheDuration);

        return value;
    }

    public async Task<byte[]?> DownloadBytesAsync(string url, CancellationToken ct = default)
    {
        using var response = await SendWithRetryAsync(url, ct);
        if (response is null)
            return null;

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<HttpResponseMessage?> SendWithRetryAsync(string url, CancellationToken ct)
    {
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.IsSuccessStatusCode)
                    return response;

                if (!ShouldRetry(response.StatusCode) || attempt == maxAttempts)
                {
                    _logger.LogWarning(
                        "Remote request failed with status code {StatusCode} for {Url}",
                        (int)response.StatusCode,
                        url);
                    response.Dispose();
                    return null;
                }

                _logger.LogWarning(
                    "Transient response {StatusCode} for {Url}. Retrying attempt {Attempt} of {MaxAttempts}",
                    (int)response.StatusCode,
                    url,
                    attempt + 1,
                    maxAttempts);

                response.Dispose();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogWarning("Remote request timed out for {Url}", url);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogWarning(ex, "Remote request failed for {Url}", url);
                    return null;
                }

                _logger.LogWarning(
                    ex,
                    "Transient network failure for {Url}. Retrying attempt {Attempt} of {MaxAttempts}",
                    url,
                    attempt + 1,
                    maxAttempts);
            }

            await Task.Delay(GetRetryDelay(attempt), ct);
        }

        return null;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        int code = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == (HttpStatusCode)429 ||
               code >= 500;
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        int baseDelayMs = 180 * attempt * attempt;
        int jitterMs = Random.Shared.Next(40, 140);
        return TimeSpan.FromMilliseconds(baseDelayMs + jitterMs);
    }
}
