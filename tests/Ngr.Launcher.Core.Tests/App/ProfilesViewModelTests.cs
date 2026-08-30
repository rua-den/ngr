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
    public async Task Profile_editor_persists_step_order_and_delays()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-profile-vm-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveToolAsync(null, Command("two"));
            var viewModel = new ProfilesViewModel(
                workspace,
                new AlwaysConfirm(),
                new InlineUiDispatcher());

            viewModel.Id = "startup";
            viewModel.Name = "Startup";
            viewModel.AddStep();
            viewModel.Steps[0].ToolId = "one";
            viewModel.Steps[0].DelayBeforeSeconds = 2;
            viewModel.AddStep();
            viewModel.Steps[1].ToolId = "two";
            viewModel.Steps[1].DelayBeforeSeconds = 7;
            viewModel.SelectedStep = viewModel.Steps[1];
            viewModel.MoveSelectedStepUp();

            await viewModel.SaveAsync();

            var profile = Assert.Single(workspace.Configuration.Profiles);
            Assert.Equal(new[] { "two", "one" }, profile.Steps.Select(step => step.ToolId));
            Assert.Equal(new[] { 7, 2 }, profile.Steps.Select(step => step.DelayBeforeSeconds));
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

    private sealed class AlwaysConfirm : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }
}
