using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using MediaTracker.Data;
using MediaTracker.Helpers;
using MediaTracker.Services;
using MediaTracker.Services.Providers;
using MediaTracker.ViewModels;

namespace MediaTracker;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureDirectories();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("Media Tracker starting up");

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            Resources["Loc"] = _serviceProvider.GetRequiredService<LocalizationService>();

            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            ShowFatalError("Media Tracker could not start correctly.\n\nCheck the log files for more details.");
            Shutdown(-1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

        // Settings
        var settings = AppSettings.Load();
        services.AddSingleton(settings);
        services.AddSingleton<LocalizationService>();

        // HTTP client
        services.AddMemoryCache();
        services.AddSingleton(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"MediaTracker/{AppVersionHelper.GetCurrentVersion()}");
            return client;
        });

        // Services
        services.AddSingleton<ResilientHttpService>();
        services.AddSingleton<AppUpdateService>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<ImageCacheService>();

        // Metadata providers
        services.AddSingleton<IMetadataProvider>(sp =>
            new TmdbProvider(
                sp.GetRequiredService<ResilientHttpService>(),
                sp.GetRequiredService<AppSettings>(),
                sp.GetRequiredService<LocalizationService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TmdbProvider>>()));
        services.AddSingleton<IMetadataProvider>(sp =>
            new RawgProvider(
                sp.GetRequiredService<ResilientHttpService>(),
                sp.GetRequiredService<AppSettings>(),
                sp.GetRequiredService<LocalizationService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RawgProvider>>()));

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = new MainWindow();
            window.DataContext = sp.GetRequiredService<MainViewModel>();
            return window;
        });

        services.AddLogging(builder => builder.AddSerilog(dispose: true));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Media Tracker shutting down");
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        ShowFatalError("An unexpected error happened in the app.\n\nYour data is still on disk. Please reopen the app and check the logs if this keeps happening.");
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "Unhandled domain exception");
        else
            Log.Fatal("Unhandled domain exception: {ExceptionObject}", e.ExceptionObject);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private static void ShowFatalError(string message)
    {
        MessageBox.Show(
            message,
            "Media Tracker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
