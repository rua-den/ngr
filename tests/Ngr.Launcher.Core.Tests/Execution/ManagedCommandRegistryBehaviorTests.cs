using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ManagedCommandRegistryBehaviorTests
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
    public void Stop_all_cancels_sessions_and_kills_only_registered_commands()
    {
        var coordinator = new FakeSessionCoordinator();
        var registry = new ManagedCommandRegistry(coordinator);
        var command = new FakeProcess();
        var application = new FakeProcess();
        registry.Register("tool", command);
        registry.RegisterApplication("app", application);

        registry.StopAll();

        Assert.True(coordinator.Cancelled);
        Assert.True(command.Killed);
        Assert.False(application.Killed);
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
