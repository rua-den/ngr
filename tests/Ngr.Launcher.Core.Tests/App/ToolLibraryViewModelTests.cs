using Ngr.Launcher.App.Services;
using Ngr.Launcher.App.ViewModels;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class ToolLibraryViewModelTests
{
    [Fact]
    public async Task Template_can_be_edited_saved_and_launched()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-vm-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var spawner = new RecordingSpawner();
            var viewModel = new ToolLibraryViewModel(
                workspace,
                spawner,
                new AlwaysConfirm(),
                new InlineUiDispatcher());

            viewModel.SelectedTemplate = viewModel.Templates.Single(template => template.Key == "npm");
            viewModel.ApplySelectedTemplate();
            viewModel.Id = "web-dev";
            viewModel.Name = "Web dev";
            viewModel.CommandText = "npm run dev -- --host";

            await viewModel.SaveAsync();
            viewModel.LaunchCurrent();

            var saved = Assert.Single(workspace.Configuration.Tools);
            Assert.Equal("web-dev", saved.Id);
            Assert.Equal("npm run dev -- --host", saved.CommandText);
            Assert.Equal("web-dev", Assert.Single(spawner.Started).Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Delete_respects_confirmation()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-delete-vm-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            var confirmation = new ToggleConfirmation { Result = false };
            var viewModel = new ToolLibraryViewModel(
                workspace,
                new RecordingSpawner(),
                confirmation,
                new InlineUiDispatcher());
            viewModel.SelectedTool = Assert.Single(viewModel.Tools);

            await viewModel.DeleteAsync();
            Assert.Single(workspace.Configuration.Tools);

            confirmation.Result = true;
            await viewModel.DeleteAsync();
            Assert.Empty(workspace.Configuration.Tools);
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

    private sealed class RecordingSpawner : ICommandSpawner
    {
        public List<ToolDefinition> Started { get; } = [];

        public CommandInstance Start(ToolDefinition tool)
        {
            Started.Add(tool);
            return new CommandInstance(tool.Id, Guid.NewGuid());
        }
    }

    private sealed class AlwaysConfirm : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }

    private sealed class ToggleConfirmation : IConfirmationService
    {
        public bool Result { get; set; }

        public bool Confirm(string title, string message) => Result;
    }
}
