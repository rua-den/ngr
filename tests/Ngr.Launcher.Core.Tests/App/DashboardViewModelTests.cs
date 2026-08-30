using Ngr.Launcher.App.Services;
using Ngr.Launcher.App.ViewModels;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task Launch_records_completed_session_and_step_results()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-dashboard-vm-").FullName;
        try
        {
            var workspace = new LauncherWorkspace(new JsonConfigurationStore(directory));
            await workspace.InitializeAsync();
            await workspace.SaveToolAsync(null, Command("one"));
            await workspace.SaveProfileAsync(null, new ProfileDefinition
            {
                Id = "profile",
                Name = "Profile",
                Steps = [new ProfileStep { ToolId = "one" }]
            });
            var spawner = new RecordingSpawner();
            var viewModel = new DashboardViewModel(
                workspace,
                new ProfileRunner(spawner, new ImmediateDelay()),
                new ProfileCancellationRegistry(),
                new InlineUiDispatcher());

            await viewModel.LaunchAsync(Assert.Single(workspace.Configuration.Profiles));

            var session = Assert.Single(viewModel.Sessions);
            Assert.Equal("Completed", session.Status);
            Assert.False(session.CanCancel);
            Assert.Equal("Started", Assert.Single(session.Steps).Status);
            Assert.Equal("one", Assert.Single(spawner.Started));
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
        public List<string> Started { get; } = [];

        public CommandInstance Start(ToolDefinition tool)
        {
            Started.Add(tool.Id);
            return new CommandInstance(tool.Id, Guid.NewGuid());
        }
    }

    private sealed class ImmediateDelay : IDelay
    {
        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
