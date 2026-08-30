using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Management;

public sealed class LauncherWorkspaceTests
{
    [Fact]
    public async Task Tool_and_profile_edits_persist_and_tool_renames_update_profile_references()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-workspace-").FullName;
        try
        {
            var store = new JsonConfigurationStore(directory);
            var workspace = new LauncherWorkspace(store);
            await workspace.InitializeAsync();

            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveProfileAsync(null, new ProfileDefinition
            {
                Id = "daily",
                Name = "Daily",
                Steps =
                [
                    new ProfileStep { ToolId = "one", DelayBeforeSeconds = 3 }
                ]
            });

            await workspace.SaveToolAsync("one", Command("two") with { Name = "Two" });

            Assert.Equal("two", Assert.Single(workspace.Configuration.Tools).Id);
            Assert.Equal(
                "two",
                Assert.Single(Assert.Single(workspace.Configuration.Profiles).Steps).ToolId);

            var reloaded = await store.LoadAsync();
            Assert.Equal(workspace.Configuration, reloaded.Configuration);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Removing_a_referenced_tool_is_blocked_without_mutating_configuration()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-workspace-delete-").FullName;
        try
        {
            var store = new JsonConfigurationStore(directory);
            var workspace = new LauncherWorkspace(store);
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveProfileAsync(null, new ProfileDefinition
            {
                Id = "profile",
                Name = "Profile",
                Steps = [new ProfileStep { ToolId = "one" }]
            });
            var before = workspace.Configuration;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => workspace.RemoveToolAsync("one"));

            Assert.Contains("Profile", exception.Message, StringComparison.Ordinal);
            Assert.Equal(before, workspace.Configuration);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Settings_changes_are_persisted_immediately()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-workspace-settings-").FullName;
        try
        {
            var store = new JsonConfigurationStore(directory);
            var workspace = new LauncherWorkspace(store);
            await workspace.InitializeAsync();
            var settings = workspace.Configuration.Settings with
            {
                Theme = ThemePreference.Dark,
                StartupPromptAnswered = true
            };

            await workspace.UpdateSettingsAsync(settings);

            var reloaded = await store.LoadAsync();
            Assert.Equal(settings, reloaded.Configuration.Settings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ToolDefinition Command(string id) => new()
    {
        Id = id,
        Name = id,
        Kind = ToolKind.Command,
        CommandText = $"echo {id}",
        Shell = ShellKind.CommandPrompt,
        WindowMode = CommandWindowMode.Hidden
    };
}
