using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ProfileRunnerBehaviorTests
{
    [Fact]
    public async Task Run_snapshots_definitions_preserves_order_and_delays_before_each_step()
    {
        var tools = new List<ToolDefinition> { Command("one"), Command("two") };
        var profile = Profile("p", new ProfileStep { ToolId = "one", DelayBeforeSeconds = 1 },
            new ProfileStep { ToolId = "two", DelayBeforeSeconds = 2 });
        var delay = new SnapshotDelay();
        var spawner = new FakeSpawner();
        var runner = new ProfileRunner(spawner, delay);

        var run = runner.RunAsync(profile, tools);
        await delay.Started;
        tools.Clear();
        delay.Release();
        var result = await run;

        Assert.Equal(new[] { "one", "two" }, spawner.StartedIds);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, delay.Requested);
        Assert.Equal(new[] { "one", "two" }, result.Steps.Select(step => step.ToolId));
    }

    [Fact]
    public async Task Run_continues_after_failure_and_returns_summary()
    {
        var spawner = new FakeSpawner { FailingId = "bad" };
        var runner = new ProfileRunner(spawner, new ImmediateDelay());

        var result = await runner.RunAsync(
            Profile("p", new ProfileStep { ToolId = "bad" }, new ProfileStep { ToolId = "good" }),
            new[] { Command("bad"), Command("good") });

        Assert.Equal(new[] { "bad", "good" }, spawner.StartedIds);
        Assert.Equal(StepRunStatus.Failed, result.Steps[0].Status);
        Assert.Equal(StepRunStatus.Started, result.Steps[1].Status);
    }

    [Fact]
    public async Task Cancellation_during_delay_marks_pending_steps_without_stopping_started_tools()
    {
        var delay = new CancellingDelay(cancelOnCall: 2);
        var spawner = new FakeSpawner();
        var runner = new ProfileRunner(spawner, delay);

        var result = await runner.RunAsync(
            Profile("p", new ProfileStep { ToolId = "one" }, new ProfileStep { ToolId = "two" },
                new ProfileStep { ToolId = "three" }),
            new[] { Command("one"), Command("two"), Command("three") });

        Assert.Equal(new[] { "one" }, spawner.StartedIds);
        Assert.Equal(
            new[] { StepRunStatus.Started, StepRunStatus.Cancelled, StepRunStatus.Cancelled },
            result.Steps.Select(step => step.Status));
    }

    [Fact]
    public async Task Same_profile_allows_concurrent_runs_with_distinct_sessions()
    {
        var runner = new ProfileRunner(new FakeSpawner(), new ImmediateDelay());
        var profile = Profile("p", new ProfileStep { ToolId = "one" });
        var tools = new[] { Command("one") };

        var results = await Task.WhenAll(runner.RunAsync(profile, tools), runner.RunAsync(profile, tools));

        Assert.NotEqual(results[0].SessionId, results[1].SessionId);
    }

    [Fact]
    public async Task Run_reports_current_step_progress()
    {
        var progress = new List<ProfileProgress>();
        var runner = new ProfileRunner(new FakeSpawner(), new ImmediateDelay());

        await runner.RunAsync(
            Profile("p", new ProfileStep { ToolId = "one" }, new ProfileStep { ToolId = "two" }),
            new[] { Command("one"), Command("two") },
            progress.Add);

        Assert.Equal(new[] { "one", "two" }, progress.Select(item => item.ToolId));
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

    private static ProfileDefinition Profile(string id, params ProfileStep[] steps) => new()
    {
        Id = id,
        Name = id,
        Steps = steps
    };

    private sealed class FakeSpawner : ICommandSpawner
    {
        public string? FailingId { get; init; }
        public List<string> StartedIds { get; } = new();

        public CommandInstance Start(ToolDefinition definition)
        {
            StartedIds.Add(definition.Id);
            if (definition.Id == FailingId)
            {
                throw new InvalidOperationException("spawn failed");
            }

            return new CommandInstance(definition.Id, Guid.NewGuid());
        }
    }

    private sealed class ImmediateDelay : IDelay
    {
        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CancellingDelay(int cancelOnCall) : IDelay
    {
        private int _calls;

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            _calls++;
            return _calls == cancelOnCall
                ? Task.FromException(new OperationCanceledException(cancellationToken))
                : Task.CompletedTask;
        }
    }

    private sealed class SnapshotDelay : IDelay
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public Task Started => _started.Task;
        public List<TimeSpan> Requested { get; } = new();

        public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Requested.Add(duration);
            _calls++;
            if (_calls == 1)
            {
                _started.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }
        }

        public void Release() => _release.TrySetResult();
    }
}
