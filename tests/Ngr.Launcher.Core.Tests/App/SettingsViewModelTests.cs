using Ngr.Launcher.App.Services;
using Ngr.Launcher.App.ViewModels;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Saving_theme_persists_and_applies_it()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-settings-vm-").FullName;
        try
        {
            var store = new JsonConfigurationStore(directory);
            var workspace = new LauncherWorkspace(store);
            await workspace.InitializeAsync();
            var themeService = new RecordingThemeService();
            var viewModel = new SettingsViewModel(
                workspace,
                new InlineUiDispatcher(),
                themeService)
            {
                Theme = ThemePreference.Dark
            };

            await viewModel.SaveAsync();

            Assert.Equal(ThemePreference.Dark, workspace.Configuration.Settings.Theme);
            Assert.Equal(ThemePreference.Dark, Assert.Single(themeService.Applied));
            Assert.Equal("Settings saved", viewModel.StatusMessage);

            var reloadedWorkspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await reloadedWorkspace.InitializeAsync();
            Assert.Equal(ThemePreference.Dark, reloadedWorkspace.Configuration.Settings.Theme);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RecordingThemeService : IThemeService
    {
        public List<ThemePreference> Applied { get; } = [];

        public void Apply(ThemePreference theme) => Applied.Add(theme);
    }
}
