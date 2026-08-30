using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ProfileRunnerProgressIsolationTests
{
    [Fact]
    public async Task Progress_observer_failure_does_not_abort_profile_launch()
    {
        var spawner = new RecordingSpawner();
        var runner = new ProfileRunner(spawner, new ImmediateDelay());
        var tools = new[] { Command("one"), Command("two") };
        var profile = new ProfileDefinition
        {
            Id = "profile",
            Name = "profile",
            Steps = new[]
            {
                new ProfileStep { ToolId = "one" },
                new ProfileStep { ToolId = "two" }
            }
        };

        var result = await runner.RunAsync(
            profile,
            tools,
            _ => throw new InvalidOperationException("UI observer failed"));

        Assert.Equal(new[] { "one", "two" }, spawner.Started);
        Assert.All(result.Steps, step => Assert.Equal(StepRunStatus.Started, step.Status));
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
