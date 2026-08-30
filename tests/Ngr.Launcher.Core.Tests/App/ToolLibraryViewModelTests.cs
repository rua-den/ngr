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
            var viewModel = CreateViewModel(workspace, spawner);

            viewModel.SelectedTemplate = viewModel.Templates.Single(template => template.Key == "npm");
            viewModel.ApplySelectedTemplate();
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
    public async Task Browse_target_and_folder_make_application_ready_without_manual_paths_or_id()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-picker-").FullName;
        try
        {
            var executable = Path.Combine(directory, "Demo Tool.exe");
            await File.WriteAllTextAsync(executable, string.Empty);
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var picker = new RecordingPathPicker
            {
                Target = executable,
                Folder = directory
            };
            var viewModel = CreateViewModel(workspace, new RecordingSpawner(), picker: picker);

            viewModel.BrowseTarget();
            viewModel.BrowseWorkingDirectory();

            Assert.Equal(executable, viewModel.Target);
            Assert.Equal(directory, viewModel.WorkingDirectory);
            Assert.Equal("Demo Tool", viewModel.Name);
            Assert.Equal("demo-tool", viewModel.Id);
            Assert.True(viewModel.IsApplication);
            Assert.False(viewModel.IsCommand);
            Assert.True(viewModel.CanSave);
            Assert.True(viewModel.CanRun);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Invalid_file_system_target_disables_save_and_test_run()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-invalid-path-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var viewModel = CreateViewModel(workspace, new RecordingSpawner());

            viewModel.Name = "Missing app";
            viewModel.Target = Path.Combine(directory, "missing.exe");

            Assert.False(viewModel.CanSave);
            Assert.False(viewModel.CanRun);
            Assert.Contains("does not exist", viewModel.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Switching_kind_exposes_only_relevant_editor_mode()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-kind-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var viewModel = CreateViewModel(workspace, new RecordingSpawner());

            Assert.True(viewModel.IsApplication);
            Assert.False(viewModel.IsCommand);

            viewModel.Kind = ToolKind.Command;

            Assert.False(viewModel.IsApplication);
            Assert.True(viewModel.IsCommand);
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
                new InlineUiDispatcher(),
                new RecordingPathPicker());
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

    private static ToolLibraryViewModel CreateViewModel(
        LauncherWorkspace workspace,
        RecordingSpawner spawner,
        IPathPickerService? picker = null) =>
        new(
            workspace,
            spawner,
            new AlwaysConfirm(),
            new InlineUiDispatcher(),
            picker ?? new RecordingPathPicker());

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

    private sealed class RecordingPathPicker : IPathPickerService
    {
        public string? Target { get; init; }
        public string? Folder { get; init; }

        public string? PickApplicationTarget(string? currentPath = null) => Target;
        public string? PickFolder(string? currentPath = null) => Folder;
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
