using MediaTracker.Models;
using MediaTracker.Services;
using MediaTracker.ViewModels;

namespace MediaTracker.BasicTests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ApplyLanguageUpdatesRuntimeLanguageAndPersistsPreference()
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), $"media-tracker-settings-vm-{Guid.NewGuid():N}.json");

        try
        {
            var settings = AppSettings.Load(settingsPath);
            settings.PreferredLanguage = AppLanguage.English;

            var localization = new LocalizationService(settings);
            var vm = new SettingsViewModel(
                settings,
                localization,
                TestServices.CreateUpdateService(settings, localization));

            vm.SelectedLanguage = AppLanguage.Spanish;
            vm.ApplyLanguageCommand.Execute(null);

            Assert.Equal(AppLanguage.Spanish, localization.CurrentLanguage);
            Assert.Equal("Español", vm.CurrentLanguageDisplayName);

            var reloaded = AppSettings.Load(settingsPath);
            Assert.Equal(AppLanguage.Spanish, reloaded.PreferredLanguage);
        }
        finally
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

    [Fact]
    public void SelectTabChangesCurrentTab()
    {
        var settings = new AppSettings { PreferredLanguage = AppLanguage.English };
        var localization = new LocalizationService(settings);
        var vm = new SettingsViewModel(
            settings,
            localization,
            TestServices.CreateUpdateService(settings, localization));

        vm.SelectTabCommand.Execute(SettingsTab.Advanced);

        Assert.Equal(SettingsTab.Advanced, vm.SelectedTab);
    }
}
