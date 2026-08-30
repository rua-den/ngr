using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class StartupServicesTests
{
    [Fact]
    public void Startup_command_quotes_executable_and_marks_startup_launch()
    {
        var command = StartupLaunchArguments.BuildCommand(@"C:\Program Files\NGR Launcher\Ngr.Launcher.exe");

        Assert.Equal("\"C:\\Program Files\\NGR Launcher\\Ngr.Launcher.exe\" --startup", command);
        Assert.True(StartupLaunchArguments.IsStartupLaunch(["--STARTUP"]));
        Assert.False(StartupLaunchArguments.IsStartupLaunch(["--other"]));
    }

    [Fact]
    public async Task First_normal_launch_can_enable_windows_startup_and_persists_answer()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-onboarding-enable-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var registration = new RecordingStartupRegistration();
            var confirmation = new RecordingConfirmation { Result = true };
            var coordinator = new StartupOnboardingCoordinator(workspace, confirmation, registration);

            await coordinator.RunIfNeededAsync(startupLaunch: false);

            Assert.Equal(1, confirmation.CallCount);
            Assert.True(registration.Enabled);
            Assert.Equal(new[] { true }, registration.Changes);
            Assert.True(workspace.Configuration.Settings.StartupPromptAnswered);
            Assert.True(workspace.Configuration.Settings.StartWithWindows);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task First_normal_launch_can_decline_windows_startup_and_persists_answer()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-onboarding-decline-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var registration = new RecordingStartupRegistration { Enabled = true };
            var confirmation = new RecordingConfirmation { Result = false };
            var coordinator = new StartupOnboardingCoordinator(workspace, confirmation, registration);

            await coordinator.RunIfNeededAsync(startupLaunch: false);

            Assert.Equal(1, confirmation.CallCount);
            Assert.False(registration.Enabled);
            Assert.Equal(new[] { false }, registration.Changes);
            Assert.True(workspace.Configuration.Settings.StartupPromptAnswered);
            Assert.False(workspace.Configuration.Settings.StartWithWindows);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Startup_launch_never_prompts_or_changes_registration()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-onboarding-startup-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var registration = new RecordingStartupRegistration { Enabled = true };
            var confirmation = new RecordingConfirmation { Result = true };
            var coordinator = new StartupOnboardingCoordinator(workspace, confirmation, registration);

            await coordinator.RunIfNeededAsync(startupLaunch: true);

            Assert.Equal(0, confirmation.CallCount);
            Assert.Empty(registration.Changes);
            Assert.False(workspace.Configuration.Settings.StartupPromptAnswered);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Answered_onboarding_is_not_repeated()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-onboarding-answered-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.UpdateSettingsAsync(workspace.Configuration.Settings with
            {
                StartupPromptAnswered = true,
                StartWithWindows = false
            });
            var registration = new RecordingStartupRegistration();
            var confirmation = new RecordingConfirmation { Result = true };
            var coordinator = new StartupOnboardingCoordinator(workspace, confirmation, registration);

            await coordinator.RunIfNeededAsync(startupLaunch: false);

            Assert.Equal(0, confirmation.CallCount);
            Assert.Empty(registration.Changes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RecordingStartupRegistration : IStartupRegistrationService
    {
        public bool Enabled { get; set; }
        public List<bool> Changes { get; } = [];

        public bool IsEnabled() => Enabled;

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            Changes.Add(enabled);
        }
    }

    private sealed class RecordingConfirmation : IConfirmationService
    {
        public bool Result { get; init; }
        public int CallCount { get; private set; }

        public bool Confirm(string title, string message)
        {
            CallCount++;
            return Result;
        }
    }
}
