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
            viewModel.AddStep();
            viewModel.Steps[0].ToolId = "one";
            viewModel.Steps[0].DelayBeforeSeconds = 2;
            viewModel.AddStep();
            viewModel.Steps[1].ToolId = "two";
            viewModel.Steps[1].DelayBeforeSeconds = 7;
            viewModel.SelectedStep = viewModel.Steps[1];
            viewModel.MoveSelectedStepUp();

            Assert.Equal("startup-stack", viewModel.Id);
            Assert.True(viewModel.CanSave);
            await viewModel.SaveAsync();

            var profile = Assert.Single(workspace.Configuration.Profiles);
            Assert.Equal("startup-stack", profile.Id);
            Assert.Equal(new[] { "two", "one" }, profile.Steps.Select(step => step.ToolId));
            Assert.Equal(new[] { 7, 2 }, profile.Steps.Select(step => step.DelayBeforeSeconds));
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

    private sealed class AlwaysConfirm : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }
}
