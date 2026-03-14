using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaTracker.Models;

namespace MediaTracker.Services;

public sealed class LocalizationService : ObservableObject
{
    private readonly AppSettings _settings;

    public static LocalizationService? Current { get; private set; }

    public LocalizationService(AppSettings settings)
    {
        _settings = settings;
        Current = this;
        ApplyLanguage(settings.PreferredLanguage, updateSettings: true, raiseNotifications: false);
    }

    public AppLanguage CurrentLanguage { get; private set; }

    public string CurrentLanguageCode => AppLanguageCatalog.GetCultureCode(CurrentLanguage);

    public CultureInfo CurrentCulture => AppLanguageCatalog.GetCulture(CurrentLanguage);

    public int LanguageVersion { get; private set; }

    public string this[string key]
    {
        get => Get(key);
        set { }
    }

    public bool SetLanguage(AppLanguage language)
    {
        if (language == CurrentLanguage)
            return false;

        ApplyLanguage(language, updateSettings: true, raiseNotifications: true);
        return true;
    }

    public string Get(string key)
    {
        if (TryGet(CurrentLanguage, key, out string? value))
            return value ?? key;

        if (TryGet(AppLanguage.English, key, out value))
            return value ?? key;

        return key;
    }

    public string Format(string key, params object?[] args)
        => string.Format(CurrentCulture, Get(key), args);

    public string GetSectionLabel(AppSection section) => Get(section switch
    {
        AppSection.Home => "nav.home",
        AppSection.Library => "nav.library",
        AppSection.Movies => "nav.movies",
        AppSection.Series => "nav.series",
        AppSection.Anime => "nav.anime",
        AppSection.Games => "nav.games",
        AppSection.Favorites => "nav.favorites",
        AppSection.Settings => "nav.settings",
        _ => "nav.home"
    });

    public string GetSectionSubtitle(AppSection section) => Get(section switch
    {
        AppSection.Home => "section.home.subtitle",
        AppSection.Library => "section.library.subtitle",
        AppSection.Movies => "section.movies.subtitle",
        AppSection.Series => "section.series.subtitle",
        AppSection.Anime => "section.anime.subtitle",
        AppSection.Games => "section.games.subtitle",
        AppSection.Favorites => "section.favorites.subtitle",
        AppSection.Settings => "section.settings.subtitle",
        _ => "section.home.subtitle"
    });

    public string GetMediaStatusLabel(MediaStatus status) => Get(status switch
    {
        MediaStatus.PlanToWatch => "status.planToWatch",
        MediaStatus.Watching => "status.watching",
        MediaStatus.Completed => "status.completed",
        MediaStatus.Paused => "status.paused",
        MediaStatus.Dropped => "status.dropped",
        _ => "status.planToWatch"
    });

    public string GetMediaTypeLabel(MediaType type) => Get(type switch
    {
        MediaType.Movie => "mediaType.movie",
        MediaType.Series => "mediaType.series",
        MediaType.Anime => "mediaType.anime",
        MediaType.Game => "mediaType.game",
        _ => "mediaType.movie"
    });

    public string GetCompletionStateLabel(CompletionState state) => Get(state switch
    {
        CompletionState.NotStarted => "completion.notStarted",
        CompletionState.InProgress => "completion.inProgress",
        CompletionState.Completed => "completion.completed",
        CompletionState.HundredPercent => "completion.hundredPercent",
        CompletionState.Abandoned => "completion.abandoned",
        _ => "completion.notStarted"
    });

    public string GetLibraryEmptyTitle(MediaType? typeFilter, bool hasSearchOrStatusFilter)
    {
        if (hasSearchOrStatusFilter)
            return Get("library.empty.filtered.title");

        return typeFilter switch
        {
            MediaType.Series => Get("library.empty.series.title"),
            MediaType.Anime => Get("library.empty.anime.title"),
            MediaType.Movie => Get("library.empty.movies.title"),
            MediaType.Game => Get("library.empty.games.title"),
            _ => Get("library.empty.default.title")
        };
    }

    public string GetLibraryEmptyMessage(bool hasSearchOrStatusFilter)
        => Get(hasSearchOrStatusFilter ? "library.empty.filtered.message" : "library.empty.default.message");

    public string GetLanguageDisplayName(AppLanguage language)
        => AppLanguageCatalog.GetNativeDisplayName(language);

    private void ApplyLanguage(AppLanguage language, bool updateSettings, bool raiseNotifications)
    {
        CurrentLanguage = language;

        if (updateSettings)
            _settings.PreferredLanguage = language;

        var culture = AppLanguageCatalog.GetCulture(language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        LanguageVersion++;

        if (!raiseNotifications)
            return;

        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged(nameof(CurrentLanguageCode));
        OnPropertyChanged(nameof(LanguageVersion));
        OnPropertyChanged("Item[]");
    }

    private static bool TryGet(AppLanguage language, string key, out string? value)
        => LocalizationResources.All[language].TryGetValue(key, out value);
}
