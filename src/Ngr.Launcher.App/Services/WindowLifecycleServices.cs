using System.Windows;
using Wpf.Ui.Tray;

namespace Ngr.Launcher.App.Services;

public interface IManagementWindow
{
    bool IsVisible { get; }

    WindowState WindowState { get; set; }

    void Show();

    void Hide();

    bool Activate();
}

public sealed class WpfManagementWindow(Window window) : IManagementWindow
{
    private readonly Window _window = window ?? throw new ArgumentNullException(nameof(window));

    public bool IsVisible => _window.IsVisible;

    public WindowState WindowState
    {
        get => _window.WindowState;
        set => _window.WindowState = value;
    }

    public void Show() => _window.Show();

    public void Hide() => _window.Hide();

    public bool Activate() => _window.Activate();
}

public sealed class WindowVisibilityController(IManagementWindow window)
{
    private readonly IManagementWindow _window = window ?? throw new ArgumentNullException(nameof(window));
    private bool _allowClose;

    public bool HandleCloseRequest()
    {
        if (_allowClose)
        {
            return false;
        }

        _window.Hide();
        return true;
    }

    public void ShowFromTray()
    {
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    public void AllowClose() => _allowClose = true;
}

public sealed class LauncherTrayIconService(Action openWindow) : NotifyIconService
{
    private readonly Action _openWindow = openWindow ?? throw new ArgumentNullException(nameof(openWindow));

    public LauncherTrayIconService(Action openWindow, string tooltipText)
        : this(openWindow)
    {
        TooltipText = tooltipText;
    }

    protected override void OnLeftClick()
    {
        base.OnLeftClick();
        _openWindow();
    }
}
