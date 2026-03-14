using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.Services.Providers;
using MediaTracker.ViewModels;

namespace MediaTracker.BasicTests;

public sealed class MainViewModelTests
{
    [Fact]
    public void ToggleInlineSearchFallsBackToManualAddWhenNoProviderIsConfigured()
    {
        bool manualAddRequested = false;
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = TestServices.CreateLocalizationService(settings);
        var vm = new MainViewModel(
            new FakeMediaService(),
            [new FakeProvider(isConfigured: false)],
            new FakeImageCacheService(),
            settings,
            localization,
            TestServices.CreateUpdateService(settings, localization),
            openManualAddOverride: () => manualAddRequested = true,
            initializeShell: false);

        vm.ToggleInlineSearchCommand.Execute(null);

        Assert.True(manualAddRequested);
        Assert.False(vm.IsInlineSearchOpen);
    }

    [Fact]
    public void ToggleInlineSearchOpensInlineSearchWhenAProviderIsConfigured()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = TestServices.CreateLocalizationService(settings);
        var vm = new MainViewModel(
            new FakeMediaService(),
            [new FakeProvider(isConfigured: true)],
            new FakeImageCacheService(),
            settings,
            localization,
            TestServices.CreateUpdateService(settings, localization),
            initializeShell: false);

        vm.ToggleInlineSearchCommand.Execute(null);

        Assert.True(vm.IsInlineSearchOpen);
    }

    [Fact]
    public void CloseInlineSearchClearsTransientInlineState()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = TestServices.CreateLocalizationService(settings);
        var vm = new MainViewModel(
            new FakeMediaService(),
            [new FakeProvider(isConfigured: true)],
            new FakeImageCacheService(),
            settings,
            localization,
            TestServices.CreateUpdateService(settings, localization),
            initializeShell: false);

        var result = new SearchResult
        {
            Title = "Hades",
            MediaType = MediaType.Game,
            ProviderName = "Fake",
            ExternalId = "99"
        };

        vm.IsInlineSearchOpen = true;
        vm.InlineSearchQuery = "Hades";
        vm.InlineResults = new ObservableCollection<SearchResult>([result]);
        vm.SelectedInlineResult = result;
        vm.ShowInlineResults = true;
        vm.ShowInlineEmpty = true;
        vm.InlineError = "search failed.";
        vm.IsInlineSearching = true;

        vm.CloseInlineSearchCommand.Execute(null);

        Assert.False(vm.IsInlineSearchOpen);
        Assert.Equal(string.Empty, vm.InlineSearchQuery);
        Assert.Empty(vm.InlineResults);
        Assert.Null(vm.SelectedInlineResult);
        Assert.False(vm.ShowInlineResults);
        Assert.False(vm.ShowInlineEmpty);
        Assert.Null(vm.InlineError);
        Assert.False(vm.IsInlineSearching);
    }

    [Fact]
    public void CurrentSectionTitleAndSubtitleUpdateWhenLanguageChanges()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = TestServices.CreateLocalizationService(settings);
        var vm = new MainViewModel(
            new FakeMediaService(),
            [new FakeProvider(isConfigured: true)],
            new FakeImageCacheService(),
            settings,
            localization,
            TestServices.CreateUpdateService(settings, localization),
            initializeShell: false);

        vm.CurrentSection = AppSection.Movies;

        Assert.Equal("Movies", vm.CurrentSectionTitle);

        localization.SetLanguage(AppLanguage.PortugueseBrazil);

        Assert.Equal("Filmes", vm.CurrentSectionTitle);
        Assert.Equal("Uma prateleira focada em filmes, notas e revisitas rápidas.", vm.CurrentSectionSubtitle);
    }

    [Fact]
    public void MainWindowKeepsSettingsNavigationEntryInSidebar()
    {
        string mainWindowPath = GetWorkspacePath("MediaTracker", "MainWindow.xaml");
        string xaml = File.ReadAllText(mainWindowPath);

        Assert.Contains("CommandParameter=\"{x:Static models:AppSection.Settings}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding [nav.settings], Source={StaticResource Loc}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
    }

    private static string GetWorkspacePath(string projectName, string fileName, [CallerFilePath] string callerFilePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerFilePath)!, "..", projectName, fileName));
}
