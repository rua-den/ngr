using System.Windows;
using Ngr.Launcher.App.Services;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class WindowVisibilityControllerTests
{
    [Fact]
    public void Close_request_hides_window_and_is_cancelled()
    {
        var window = new FakeManagementWindow { IsVisible = true };
        var controller = new WindowVisibilityController(window);

        var cancelClose = controller.HandleCloseRequest();

        Assert.True(cancelClose);
        Assert.False(window.IsVisible);
        Assert.Equal(1, window.HideCalls);
    }

    [Fact]
    public void Tray_open_restores_shows_and_activates_window()
    {
        var window = new FakeManagementWindow
        {
            IsVisible = false,
            WindowState = WindowState.Minimized
        };
        var controller = new WindowVisibilityController(window);

        controller.ShowFromTray();

        Assert.True(window.IsVisible);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(1, window.ShowCalls);
        Assert.Equal(1, window.ActivateCalls);
    }

    [Fact]
    public void Allow_close_bypasses_hide_to_tray_for_future_explicit_exit()
    {
        var window = new FakeManagementWindow { IsVisible = true };
        var controller = new WindowVisibilityController(window);
        controller.AllowClose();

        var cancelClose = controller.HandleCloseRequest();

        Assert.False(cancelClose);
        Assert.True(window.IsVisible);
        Assert.Equal(0, window.HideCalls);
    }

    private sealed class FakeManagementWindow : IManagementWindow
    {
        public bool IsVisible { get; set; }

        public WindowState WindowState { get; set; } = WindowState.Normal;

        public int ShowCalls { get; private set; }

        public int HideCalls { get; private set; }

        public int ActivateCalls { get; private set; }

        public void Show()
        {
            ShowCalls++;
            IsVisible = true;
        }

        public void Hide()
        {
            HideCalls++;
            IsVisible = false;
        }

        public bool Activate()
        {
            ActivateCalls++;
            return true;
        }
    }
}
