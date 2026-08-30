using System.Windows;
using System.Windows.Controls;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
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

    public bool IsCloseAllowed => _allowClose;

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

public interface IManagedCommandShutdown
{
    void StopAll();
}

public sealed class ManagedCommandShutdown(ManagedCommandRegistry registry) : IManagedCommandShutdown
{
    private readonly ManagedCommandRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public void StopAll() => _registry.StopAll();
}

public sealed class ApplicationExitController
{
    private readonly IConfirmationService _confirmation;
    private readonly IProfileSessionCoordinator _sessions;
    private readonly IManagedCommandShutdown _managedCommands;
    private readonly WindowVisibilityController _windowVisibility;
    private readonly Action _unregisterTray;
    private readonly Action _shutdown;
    private int _exitStarted;

    public ApplicationExitController(
        IConfirmationService confirmation,
        IProfileSessionCoordinator sessions,
        IManagedCommandShutdown managedCommands,
        WindowVisibilityController windowVisibility,
        Action unregisterTray,
        Action shutdown)
    {
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _managedCommands = managedCommands ?? throw new ArgumentNullException(nameof(managedCommands));
        _windowVisibility = windowVisibility ?? throw new ArgumentNullException(nameof(windowVisibility));
        _unregisterTray = unregisterTray ?? throw new ArgumentNullException(nameof(unregisterTray));
        _shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
    }

    public bool RequestExit()
    {
        if (Volatile.Read(ref _exitStarted) != 0)
        {
            return true;
        }

        var confirmed = _confirmation.Confirm(
            "Exit NGR Launcher",
            "Exit NGR Launcher? Pending profile steps will be cancelled and all managed command process trees will be stopped. Desktop applications launched by NGR Launcher will remain running.");
        if (!confirmed)
        {
            return false;
        }

        if (Interlocked.Exchange(ref _exitStarted, 1) != 0)
        {
            return true;
        }

        _windowVisibility.AllowClose();
        BestEffort(_sessions.CancelAll);
        BestEffort(_managedCommands.StopAll);
        BestEffort(_unregisterTray);
        _shutdown();
        return true;
    }

    private static void BestEffort(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Shutdown must continue even if one cleanup step fails.
        }
    }
}

public sealed class LauncherTrayIconService : NotifyIconService
{
    private readonly Action _openWindow;
    private readonly Action<ProfileDefinition> _launchProfile;
    private readonly Action _exit;

    public LauncherTrayIconService(
        Action openWindow,
        Action<ProfileDefinition> launchProfile,
        Action exit,
        IEnumerable<ProfileDefinition> profiles,
        string tooltipText)
    {
        _openWindow = openWindow ?? throw new ArgumentNullException(nameof(openWindow));
        _launchProfile = launchProfile ?? throw new ArgumentNullException(nameof(launchProfile));
        _exit = exit ?? throw new ArgumentNullException(nameof(exit));
        ArgumentNullException.ThrowIfNull(profiles);
        TooltipText = tooltipText ?? throw new ArgumentNullException(nameof(tooltipText));
        UpdateProfiles(profiles);
    }

    public void UpdateProfiles(IEnumerable<ProfileDefinition> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open NGR Launcher" };
        openItem.Click += (_, _) => _openWindow();
        menu.Items.Add(openItem);
        menu.Items.Add(new Separator());

        var profilesItem = new MenuItem { Header = "Profiles" };
        var profileSnapshots = profiles
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => profile with
            {
                Steps = profile.Steps.Select(step => step with { }).ToArray()
            })
            .ToArray();

        if (profileSnapshots.Length == 0)
        {
            profilesItem.Items.Add(new MenuItem
            {
                Header = "No profiles",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var profile in profileSnapshots)
            {
                var profileItem = new MenuItem
                {
                    Header = profile.Name,
                    ToolTip = profile.Id
                };
                profileItem.Click += (_, _) => _launchProfile(profile);
                profilesItem.Items.Add(profileItem);
            }
        }

        menu.Items.Add(profilesItem);
        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => _exit();
        menu.Items.Add(exitItem);

        ContextMenu = menu;
    }

    protected override void OnLeftClick()
    {
        base.OnLeftClick();
        _openWindow();
    }
}
