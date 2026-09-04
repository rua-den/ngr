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

            Assert.True(viewModel.IsDirty);
            Assert.True(viewModel.CanSave);
            await viewModel.SaveAsync();
            viewModel.LaunchCurrent();

            var saved = Assert.Single(workspace.Configuration.Tools);
            Assert.Equal("web-dev", saved.Id);
            Assert.Equal("npm run dev -- --host", saved.CommandText);
            Assert.Equal("web-dev", Assert.Single(spawner.Started).Id);
            Assert.False(viewModel.IsDirty);
            Assert.False(viewModel.CanSave);
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
            var executable = Path.Combine(directory, "demo.exe");
            await File.WriteAllTextAsync(executable, string.Empty);
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var picker = new RecordingPathPicker
            {
                Target = new ApplicationTargetSelection(executable, "Demo Tool"),
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
    public async Task Installed_app_friendly_name_is_used_instead_of_executable_file_name()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-friendly-name-").FullName;
        try
        {
            var executable = Path.Combine(directory, "chrome.exe");
            await File.WriteAllTextAsync(executable, string.Empty);
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            var picker = new RecordingPathPicker
            {
                Target = new ApplicationTargetSelection(executable, "Google Chrome")
            };
            var viewModel = CreateViewModel(workspace, new RecordingSpawner(), picker: picker);

            viewModel.BrowseTarget();

            Assert.Equal("Google Chrome", viewModel.Name);
            Assert.Equal("google-chrome", viewModel.Id);
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
    public async Task Unsaved_tool_changes_survive_unrelated_workspace_updates()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-dirty-refresh-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            var viewModel = CreateViewModel(workspace, new RecordingSpawner());
            viewModel.SelectedTool = Assert.Single(viewModel.Tools);
            viewModel.Name = "Unsaved rename";

            await workspace.SaveToolAsync(null, Command("two"));

            Assert.True(viewModel.IsDirty);
            Assert.Equal("Unsaved rename", viewModel.Name);
            Assert.Equal("one", viewModel.SelectedTool?.Id);
            Assert.Equal(2, viewModel.Tools.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Declining_discard_keeps_current_tool_and_unsaved_editor()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-tool-discard-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveToolAsync(null, Command("two"));
            var confirmation = new ToggleConfirmation { Result = false };
            var viewModel = new ToolLibraryViewModel(
                workspace,
                new RecordingSpawner(),
                confirmation,
                new InlineUiDispatcher(),
                new RecordingPathPicker());
            var first = viewModel.Tools.Single(tool => tool.Id == "one");
            var second = viewModel.Tools.Single(tool => tool.Id == "two");
            viewModel.SelectedTool = first;
            viewModel.Name = "Unsaved rename";

            viewModel.SelectedTool = second;

            Assert.Equal("one", viewModel.SelectedTool?.Id);
            Assert.Equal("Unsaved rename", viewModel.Name);
            Assert.True(viewModel.IsDirty);
            Assert.Equal(1, confirmation.CallCount);
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
        public ApplicationTargetSelection? Target { get; init; }
        public string? Folder { get; init; }

        public ApplicationTargetSelection? PickApplicationTarget(string? currentPath = null) => Target;
        public string? PickFolder(string? currentPath = null) => Folder;
    }

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
