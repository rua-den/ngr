using Ngr.Launcher.App.Services;
using Ngr.Launcher.App.ViewModels;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class ProfilesViewModelTests
{
    [Fact]
    public async Task Profile_editor_generates_id_and_persists_step_order_and_delays()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-vm-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveToolAsync(null, Command("two"));
            var viewModel = CreateViewModel(workspace);

            viewModel.Name = "Startup Stack";
            viewModel.ToolToAdd = viewModel.AvailableTools.Single(tool => tool.Id == "one");
            viewModel.AddStep();
            viewModel.Steps[0].DelayBeforeSeconds = 2;
            viewModel.ToolToAdd = viewModel.AvailableTools.Single(tool => tool.Id == "two");
            viewModel.AddStep();
            viewModel.Steps[1].DelayBeforeSeconds = 7;
            viewModel.SelectedStep = viewModel.Steps[1];
            viewModel.MoveSelectedStepUp();

            Assert.Equal("startup-stack", viewModel.Id);
            Assert.True(viewModel.IsDirty);
            Assert.True(viewModel.CanSave);
            await viewModel.SaveAsync();

            var profile = Assert.Single(workspace.Configuration.Profiles);
            Assert.Equal("startup-stack", profile.Id);
            Assert.Equal(new[] { "two", "one" }, profile.Steps.Select(step => step.ToolId));
            Assert.Equal(new[] { 7, 2 }, profile.Steps.Select(step => step.DelayBeforeSeconds));
            Assert.False(viewModel.IsDirty);
            Assert.False(viewModel.CanSave);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Add_step_uses_the_tool_chosen_in_the_add_picker()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-add-tool-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveToolAsync(null, Command("two"));
            var viewModel = CreateViewModel(workspace);
            viewModel.Name = "Stack";
            viewModel.ToolToAdd = viewModel.AvailableTools.Single(tool => tool.Id == "two");

            viewModel.AddStep();

            Assert.Equal("two", Assert.Single(viewModel.Steps).ToolId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Profile_editor_explains_that_a_tool_is_required_before_steps_can_be_added()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-empty-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var viewModel = CreateViewModel(workspace);

            viewModel.Name = "Startup";
            viewModel.AddStep();

            Assert.True(viewModel.HasNoAvailableTools);
            Assert.Empty(viewModel.Steps);
            Assert.False(viewModel.CanSave);
            Assert.Contains("tool", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Delay_outside_supported_range_disables_save_immediately()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-delay-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            var viewModel = CreateViewModel(workspace);

            viewModel.Name = "Startup";
            viewModel.AddStep();
            viewModel.Steps[0].DelayBeforeSeconds = 301;

            Assert.False(viewModel.CanSave);
            Assert.Contains("300", viewModel.ValidationMessage, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Unsaved_profile_changes_survive_unrelated_workspace_updates()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-dirty-refresh-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveProfileAsync(null, Profile("profile-one", "one"));
            var viewModel = CreateViewModel(workspace);
            viewModel.SelectedProfile = Assert.Single(viewModel.Profiles);
            viewModel.Name = "Unsaved profile rename";

            await workspace.SaveToolAsync(null, Command("two"));

            Assert.True(viewModel.IsDirty);
            Assert.Equal("Unsaved profile rename", viewModel.Name);
            Assert.Equal("profile-one", viewModel.SelectedProfile?.Id);
            Assert.Equal(2, viewModel.AvailableTools.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Declining_discard_keeps_current_profile_and_unsaved_editor()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-discard-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveProfileAsync(null, Profile("profile-one", "one"));
            await workspace.SaveProfileAsync(null, Profile("profile-two", "one"));
            var confirmation = new ToggleConfirmation { Result = false };
            var viewModel = new ProfilesViewModel(workspace, confirmation, new InlineUiDispatcher());
            var first = viewModel.Profiles.Single(profile => profile.Id == "profile-one");
            var second = viewModel.Profiles.Single(profile => profile.Id == "profile-two");
            viewModel.SelectedProfile = first;
            viewModel.Name = "Unsaved profile rename";

            viewModel.SelectedProfile = second;

            Assert.Equal("profile-one", viewModel.SelectedProfile?.Id);
            Assert.Equal("Unsaved profile rename", viewModel.Name);
            Assert.True(viewModel.IsDirty);
            Assert.Equal(1, confirmation.CallCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ProfilesViewModel CreateViewModel(LauncherWorkspace workspace) =>
        new(workspace, new AlwaysConfirm(), new InlineUiDispatcher());

    private static ToolDefinition Command(string id) => new()
    {
        Id = id,
        Name = id,
        Kind = ToolKind.Command,
        CommandText = $"echo {id}",
        Shell = ShellKind.CommandPrompt,
        WindowMode = CommandWindowMode.Hidden
    };

    private static ProfileDefinition Profile(string id, string toolId) => new()
    {
        Id = id,
        Name = id,
        Steps = [new ProfileStep { ToolId = toolId }]
    };

    private sealed class AlwaysConfirm : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }

    private sealed class ToggleConfirmation : IConfirmationService
    {
        public bool Result { get; set; }
        public int CallCount { get; private set; }

        public bool Confirm(string title, string message)
        {
            CallCount++;
            return Result;
        }
    }
}
