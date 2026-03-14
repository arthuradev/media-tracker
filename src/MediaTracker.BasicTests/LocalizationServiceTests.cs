using System.ComponentModel;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.BasicTests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void ReturnsTranslationsForSupportedLanguages()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);

        Assert.Equal("Home", localization.Get("nav.home"));

        localization.SetLanguage(AppLanguage.PortugueseBrazil);
        Assert.Equal("Inicio", localization.Get("nav.home"));

        localization.SetLanguage(AppLanguage.Spanish);
        Assert.Equal("Inicio", localization.Get("nav.home"));
    }

    [Fact]
    public void FallsBackToEnglishWhenKeyIsMissingInCurrentLocale()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.PortugueseBrazil };
        var localization = new LocalizationService(settings);

        Assert.Equal("Fallback works", localization.Get("test.fallbackOnly"));
    }

    [Fact]
    public void RaisesPropertyChangedWhenLanguageChanges()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);
        var changedProperties = new List<string>();

        localization.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
                changedProperties.Add(args.PropertyName);
        };

        localization.SetLanguage(AppLanguage.Spanish);

        Assert.Contains(nameof(LocalizationService.CurrentLanguage), changedProperties);
        Assert.Contains(nameof(LocalizationService.CurrentLanguageCode), changedProperties);
        Assert.Contains("Item[]", changedProperties);
    }
}
