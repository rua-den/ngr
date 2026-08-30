using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ProfileRunnerTests
{
    [Fact]
    public async Task Run_snapshots_definitions_preserves_order_and_delays_before_each_step()
    {
        var tools = new List<ToolDefinition>
        {
            new("one", "cmd", "one"), new("two", "cmd", "two")
        };
        var profile = new ProfileDefinition("p", new[] { new ProfileStep("one"), new ProfileStep("two") });
        var delay = new FakeDelay();
        var spawner = new FakeSpawner();
        var runner = new ProfileRunner(spawner, delay);

        var task = runner.RunAsync(profile, tools);
        tools.Clear();
        var result = await task;

        Assert.Equal(new[] { "one", "two" }, spawner.StartedIds);
        Assert.Equal(2, delay.Delays);
        Assert.Equal(new[] { "one", "two" }, result.Steps.Select(x => x.ToolId));
    }

    [Fact]
    public async Task Run_continues_after_failed_spawn_and_returns_started_failed_summary()
    {
        var spawner = new FakeSpawner { FailingId = "bad" };
        var runner = new ProfileRunner(spawner, new FakeDelay());
        var result = await runner.RunAsync(
            new ProfileDefinition("p", new[] { new ProfileStep("bad"), new ProfileStep("good") }),
            new[] { new ToolDefinition("bad", "x", ""), new ToolDefinition("good", "x", "") });

        Assert.Equal(new[] { "bad", "good" }, spawner.StartedIds);
        Assert.Equal(StepRunStatus.Failed, result.Steps[0].Status);
        Assert.Equal(StepRunStatus.Started, result.Steps[1].Status);
    }

    [Fact]
    public async Task Cancellation_during_delay_marks_current_and_remaining_cancelled_without_stopping_launched_tools()
    {
        var delay = new FakeDelay { CancelOnCall = 2 };
        var spawner = new FakeSpawner();
        var runner = new ProfileRunner(spawner, delay);

        var result = await runner.RunAsync(
            new ProfileDefinition("p", new[] { new ProfileStep("one"), new ProfileStep("two"), new ProfileStep("three") }),
            new[] { new ToolDefinition("one", "x", ""), new ToolDefinition("two", "x", ""), new ToolDefinition("three", "x", "") });

        Assert.Equal(new[] { "one" }, spawner.StartedIds);
        Assert.Equal(new[] { StepRunStatus.Started, StepRunStatus.Cancelled, StepRunStatus.Cancelled }, result.Steps.Select(x => x.Status));
    }

    [Fact]
    public async Task Same_profile_allows_simultaneous_runs_with_distinct_session_ids()
    {
        var runner = new ProfileRunner(new FakeSpawner(), new FakeDelay());
        var profile = new ProfileDefinition("p", new[] { new ProfileStep("one") });
        var tools = new[] { new ToolDefinition("one", "x", "") };

        var results = await Task.WhenAll(runner.RunAsync(profile, tools), runner.RunAsync(profile, tools));

        Assert.NotEqual(results[0].SessionId, results[1].SessionId);
    }

    [Fact]
    public async Task Run_reports_current_step_progress()
    {
        var progress = new List<ProfileProgress>();
        var runner = new ProfileRunner(new FakeSpawner(), new FakeDelay());

        await runner.RunAsync(new ProfileDefinition("p", new[] { new ProfileStep("one"), new ProfileStep("two") }),
            new[] { new ToolDefinition("one", "x", ""), new ToolDefinition("two", "x", "") }, progress.Add);

        Assert.Equal(new[] { "one", "two" }, progress.Select(x => x.ToolId));
    }

    private sealed class FakeSpawner : ICommandSpawner
    {
        public string? FailingId { get; init; }
        public List<string> StartedIds { get; } = new();
        public CommandInstance Start(ToolDefinition definition) { StartedIds.Add(definition.Id); if (definition.Id == FailingId) throw new InvalidOperationException(); return new CommandInstance(definition.Id, Guid.NewGuid()); }
    }

    private sealed class FakeDelay : IDelay
    {
        public int Delays { get; private set; }
        public int? CancelOnCall { get; init; }
        public Task DelayAsync(CancellationToken cancellationToken) { Delays++; if (CancelOnCall == Delays) throw new OperationCanceledException(); return Task.CompletedTask; }
    }
}
