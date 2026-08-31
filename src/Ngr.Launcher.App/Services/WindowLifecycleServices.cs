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
        var menu = CreateContextMenu();

        var openItem = CreateMenuItem("Open NGR Launcher");
        openItem.FontWeight = FontWeights.SemiBold;
        openItem.Click += (_, _) => _openWindow();
        menu.Items.Add(openItem);
        menu.Items.Add(CreateSeparator());

        var profileSnapshots = profiles
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => profile with
            {
                Steps = profile.Steps.Select(step => step with { }).ToArray()
            })
            .ToArray();

        if (profileSnapshots.Length == 0)
        {
            var empty = CreateMenuItem("No profiles yet");
            empty.IsEnabled = false;
            menu.Items.Add(empty);
        }
        else
        {
            var section = CreateMenuItem("RUN PROFILE");
            section.IsEnabled = false;
            section.FontSize = 10.5;
            menu.Items.Add(section);

            foreach (var profile in profileSnapshots)
            {
                var profileItem = CreateMenuItem(profile.Name);
                profileItem.ToolTip = $"Run profile: {profile.Name}";
                profileItem.Click += (_, _) => _launchProfile(profile);
                menu.Items.Add(profileItem);
            }
        }

        menu.Items.Add(CreateSeparator());
        var exitItem = CreateMenuItem("Exit NGR Launcher");
        exitItem.Foreground = System.Windows.Media.Brushes.IndianRed;
        exitItem.Click += (_, _) => _exit();
        menu.Items.Add(exitItem);
        ContextMenu = menu;
    }

    protected override void OnLeftClick()
    {
        base.OnLeftClick();
        _openWindow();
    }

    private static ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu
        {
            MinWidth = 220,
            Padding = new Thickness(5),
            BorderThickness = new Thickness(1),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12.5
        };
        menu.SetResourceReference(Control.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        menu.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        menu.SetResourceReference(Control.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        return menu;
    }

    private static MenuItem CreateMenuItem(string header)
    {
        var item = new MenuItem
        {
            Header = header,
            Padding = new Thickness(11, 7, 11, 7),
            Margin = new Thickness(0, 1, 0, 1)
        };
        item.SetResourceReference(Control.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        item.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        return item;
    }

    private static Separator CreateSeparator()
    {
        var separator = new Separator { Margin = new Thickness(4) };
        separator.SetResourceReference(Control.BackgroundProperty, "DividerStrokeColorDefaultBrush");
        return separator;
    }
}
