using Ngr.Launcher.App.Services;
using Ngr.Launcher.App.ViewModels;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Saving_theme_persists_applies_it_and_keeps_actual_startup_state()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-settings-vm-").FullName;
        try
        {
            var store = new JsonConfigurationStore(directory);
            var workspace = new LauncherWorkspace(store);
            await workspace.InitializeAsync();
            var themeService = new RecordingThemeService();
            var startup = new RecordingStartupRegistration();
            var viewModel = new SettingsViewModel(
                workspace,
                new InlineUiDispatcher(),
                themeService,
                startup)
            {
                Theme = ThemePreference.Dark
            };

            await viewModel.SaveAsync();

            Assert.Equal(ThemePreference.Dark, workspace.Configuration.Settings.Theme);
            Assert.Equal(ThemePreference.Dark, Assert.Single(themeService.Applied));
            Assert.False(workspace.Configuration.Settings.StartWithWindows);
            Assert.False(startup.Enabled);
            Assert.Contains("Windows startup is disabled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var reloadedWorkspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await reloadedWorkspace.InitializeAsync();
            Assert.Equal(ThemePreference.Dark, reloadedWorkspace.Configuration.Settings.Theme);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Enabling_startup_updates_registration_and_persists_setting()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-settings-startup-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var startup = new RecordingStartupRegistration();
            var viewModel = new SettingsViewModel(
                workspace,
                new InlineUiDispatcher(),
                new RecordingThemeService(),
                startup)
            {
                StartWithWindows = true
            };

            await viewModel.SaveAsync();

            Assert.True(startup.Enabled);
            Assert.Equal(new[] { true }, startup.Changes);
            Assert.True(workspace.Configuration.Settings.StartWithWindows);
            Assert.True(workspace.Configuration.Settings.StartupPromptAnswered);
            Assert.Contains("start hidden", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Startup_registration_failure_does_not_claim_or_persist_success()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-settings-startup-failure-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var startup = new RecordingStartupRegistration { ThrowOnSet = true };
            var viewModel = new SettingsViewModel(
                workspace,
                new InlineUiDispatcher(),
                new RecordingThemeService(),
                startup)
            {
                StartWithWindows = true
            };

            await viewModel.SaveAsync();

            Assert.False(workspace.Configuration.Settings.StartWithWindows);
            Assert.False(workspace.Configuration.Settings.StartupPromptAnswered);
            Assert.Contains("Could not update Windows startup", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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

    private sealed class RecordingStartupRegistration : IStartupRegistrationService
    {
        public bool Enabled { get; set; }
        public bool ThrowOnSet { get; init; }
        public List<bool> Changes { get; } = [];

        public bool IsEnabled() => Enabled;

        public void SetEnabled(bool enabled)
        {
            if (ThrowOnSet)
            {
                throw new InvalidOperationException("registry denied");
            }

            Enabled = enabled;
            Changes.Add(enabled);
        }
    }
}
