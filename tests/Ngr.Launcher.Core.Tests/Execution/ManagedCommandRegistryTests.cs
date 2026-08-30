using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ManagedCommandRegistryTests
{
    [Fact]
    public void Registers_duplicates_removes_on_exit_and_returns_only_live_instances()
    {
        var registry = new ManagedCommandRegistry();
        var first = new FakeProcess();
        var second = new FakeProcess();
        registry.Register("same", first);
        registry.Register("same", second);
        first.Exit();

        Assert.Same(second, Assert.Single(registry.Live("same")));
    }

    [Fact]
    public void StopAll_cancels_sessions_kills_live_command_trees_and_excludes_applications()
    {
        var coordinator = new FakeSessionCoordinator();
        var registry = new ManagedCommandRegistry(coordinator);
        var process = new FakeProcess();
        registry.Register("tool", process);
        registry.RegisterApplication("app", new FakeProcess());

        registry.StopAll();

        Assert.True(coordinator.Cancelled);
        Assert.True(process.Killed);
    }

    private sealed class FakeProcess : IManagedProcess
    {
        public bool Killed { get; private set; }
        public event EventHandler? Exited;
        public void KillTree() => Killed = true;
        public void Exit() => Exited?.Invoke(this, EventArgs.Empty);
    }
    private sealed class FakeSessionCoordinator : IProfileSessionCoordinator
    {
        public bool Cancelled { get; private set; }
        public void CancelAll() => Cancelled = true;
    }
}
