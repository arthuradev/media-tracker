using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MediaTracker.Data;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;

namespace MediaTracker.BasicTests;

internal static class TestServices
{
    public static LocalizationService CreateLocalizationService(AppSettings? settings = null)
    {
        settings ??= new AppSettings { PreferredLanguage = AppLanguage.English };
        return new LocalizationService(settings);
    }

    public static AppUpdateService CreateUpdateService(AppSettings? settings = null, LocalizationService? localization = null)
    {
        localization ??= CreateLocalizationService(settings);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var resilientHttp = new ResilientHttpService(new HttpClient(), cache, new FakeLogger<ResilientHttpService>());
        return new AppUpdateService(resilientHttp, localization, new FakeLogger<AppUpdateService>());
    }
}

internal sealed class TestHarness : IAsyncDisposable
{
    private readonly string _databasePath;

    private TestHarness(string databasePath, TestDbContextFactory dbFactory)
    {
        _databasePath = databasePath;
        DbFactory = dbFactory;
        MediaService = new MediaService(dbFactory);
    }

    public TestDbContextFactory DbFactory { get; }
    public MediaService MediaService { get; }

    public static async Task<TestHarness> CreateAsync()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"media-tracker-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var factory = new TestDbContextFactory(options);

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        return new TestHarness(databasePath, factory);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch
        {
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppDbContext(_options));
}

internal sealed class FakeProvider : IMetadataProvider
{
    private readonly bool _isConfigured;

    public FakeProvider(bool isConfigured)
    {
        _isConfigured = isConfigured;
    }

    public string Name => "Fake";
    public bool IsConfigured => _isConfigured;
    public string ConfigurationHint => "Add the fake provider key first.";
    public MediaType[] SupportedTypes => [MediaType.Game];
    public int SearchCalls { get; private set; }

    public Task<List<SearchResult>> SearchAsync(string query, MediaType mediaType, CancellationToken ct = default)
    {
        SearchCalls++;
        return Task.FromResult(new List<SearchResult>());
    }

    public Task<SearchResult?> GetDetailsAsync(string externalId, MediaType mediaType, CancellationToken ct = default)
        => Task.FromResult<SearchResult?>(null);

    public Task<List<EpisodeResult>> GetEpisodesAsync(string externalId, int seasonNumber, CancellationToken ct = default)
        => Task.FromResult(new List<EpisodeResult>());
}

internal sealed class FakeImageCacheService : ImageCacheService
{
    public FakeImageCacheService() : base(new FakeResilientHttpService(), new FakeLogger<ImageCacheService>())
    {
    }
}

internal sealed class FakeMediaService : MediaService
{
    public FakeMediaService() : base(new TestDbContextFactory(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options))
    {
    }
}

internal sealed class FakeResilientHttpService : ResilientHttpService
{
    public FakeResilientHttpService() : base(
        new HttpClient(),
        new MemoryCache(new MemoryCacheOptions()),
        new FakeLogger<ResilientHttpService>())
    {
    }
}

internal sealed class FakeLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
    }
}

internal sealed class SequenceHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public SequenceHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_respond(request));
}

internal sealed class TestPayload
{
    public string? Value { get; set; }
}
