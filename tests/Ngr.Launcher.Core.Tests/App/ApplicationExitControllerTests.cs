using System.Windows;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class ApplicationExitControllerTests
{
    [Fact]
    public void Declined_exit_has_no_cleanup_side_effects()
    {
        var confirmation = new ToggleConfirmation { Result = false };
        var sessions = new RecordingSessions();
        var commands = new RecordingCommandShutdown();
        var visibility = new WindowVisibilityController(new FakeManagementWindow());
        var unregisterCalls = 0;
        var shutdownCalls = 0;
        var controller = new ApplicationExitController(
            confirmation,
            sessions,
            commands,
            visibility,
            () => unregisterCalls++,
            () => shutdownCalls++);

        var started = controller.RequestExit();

        Assert.False(started);
        Assert.Equal(1, confirmation.Calls);
        Assert.Equal(0, sessions.CancelAllCalls);
        Assert.Equal(0, commands.StopAllCalls);
        Assert.Equal(0, unregisterCalls);
        Assert.Equal(0, shutdownCalls);
        Assert.False(visibility.IsCloseAllowed);
    }

    [Fact]
    public void Confirmed_exit_allows_close_cancels_sessions_stops_commands_and_shuts_down()
    {
        var confirmation = new ToggleConfirmation { Result = true };
        var sessions = new RecordingSessions();
        var commands = new RecordingCommandShutdown();
        var visibility = new WindowVisibilityController(new FakeManagementWindow());
        var unregisterCalls = 0;
        var shutdownCalls = 0;
        var controller = new ApplicationExitController(
            confirmation,
            sessions,
            commands,
            visibility,
            () => unregisterCalls++,
            () => shutdownCalls++);

        var started = controller.RequestExit();

        Assert.True(started);
        Assert.True(visibility.IsCloseAllowed);
        Assert.Equal(1, sessions.CancelAllCalls);
        Assert.Equal(1, commands.StopAllCalls);
        Assert.Equal(1, unregisterCalls);
        Assert.Equal(1, shutdownCalls);
    }

    [Fact]
    public void Confirmed_exit_is_idempotent_after_shutdown_starts()
    {
        var confirmation = new ToggleConfirmation { Result = true };
        var sessions = new RecordingSessions();
        var commands = new RecordingCommandShutdown();
        var visibility = new WindowVisibilityController(new FakeManagementWindow());
        var unregisterCalls = 0;
        var shutdownCalls = 0;
        var controller = new ApplicationExitController(
            confirmation,
            sessions,
            commands,
            visibility,
            () => unregisterCalls++,
            () => shutdownCalls++);

        Assert.True(controller.RequestExit());
        Assert.True(controller.RequestExit());

        Assert.Equal(1, confirmation.Calls);
        Assert.Equal(1, sessions.CancelAllCalls);
        Assert.Equal(1, commands.StopAllCalls);
        Assert.Equal(1, unregisterCalls);
        Assert.Equal(1, shutdownCalls);
    }

    private sealed class ToggleConfirmation : IConfirmationService
    {
        public bool Result { get; set; }

        public int Calls { get; private set; }

        public bool Confirm(string title, string message)
        {
            Calls++;
            return Result;
        }
    }

    private sealed class RecordingSessions : IProfileSessionCoordinator
    {
        public int CancelAllCalls { get; private set; }

        public void CancelAll() => CancelAllCalls++;
    }

    private sealed class RecordingCommandShutdown : IManagedCommandShutdown
    {
        public int StopAllCalls { get; private set; }

        public void StopAll() => StopAllCalls++;
    }

    private sealed class FakeManagementWindow : IManagementWindow
    {
        public bool IsVisible { get; private set; } = true;

        public WindowState WindowState { get; set; } = WindowState.Normal;

        public void Show() => IsVisible = true;

        public void Hide() => IsVisible = false;

        public bool Activate() => true;
    }
}
