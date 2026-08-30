using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ManagedCommandRegistryShutdownTests
{
    [Fact]
    public void Registration_after_shutdown_is_killed_immediately_and_never_becomes_live()
    {
        var coordinator = new FakeCoordinator();
        var registry = new ManagedCommandRegistry(coordinator);
        registry.StopAll();
        var lateProcess = new FakeProcess();

        registry.Register("late", lateProcess);

        Assert.True(coordinator.Cancelled);
        Assert.True(lateProcess.Killed);
        Assert.Empty(registry.Live("late"));
    }

    private sealed class FakeCoordinator : IProfileSessionCoordinator
    {
        public bool Cancelled { get; private set; }

        public void CancelAll() => Cancelled = true;
    }

    private sealed class FakeProcess : IManagedProcess
    {
        public bool Killed { get; private set; }
        public event EventHandler? Exited;

        public void KillTree()
        {
            Killed = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }
}
