using System.ComponentModel;
using System.IO;
using System.Windows;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.App.ViewModels;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Management;

namespace Ngr.Launcher.App;

public partial class App : System.Windows.Application
{
    private LauncherTrayIconService? _trayIcon;
    private WindowVisibilityController? _windowVisibility;
    private ApplicationExitController? _exitController;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NGR Launcher");
            var store = new JsonConfigurationStore(dataDirectory);
            var workspace = new LauncherWorkspace(store);
            var loadResult = await workspace.InitializeAsync();

            var cancellations = new ProfileCancellationRegistry();
            var registry = new ManagedCommandRegistry(cancellations);
            var spawner = new SystemCommandSpawner(registry, Path.Combine(dataDirectory, "Logs"));
            var runner = new ProfileRunner(spawner, new SystemDelay());
            var confirmation = new WpfConfirmationService();
            var dispatcher = new WpfUiDispatcher();
            var themeService = new WpfThemeService();
            themeService.Apply(workspace.Configuration.Settings.Theme);

            var dashboard = new DashboardViewModel(workspace, runner, cancellations, dispatcher);
            var tools = new ToolLibraryViewModel(workspace, spawner, confirmation, dispatcher);
            var profiles = new ProfilesViewModel(workspace, confirmation, dispatcher);
            var settings = new SettingsViewModel(workspace, dispatcher, themeService);
            var mainViewModel = new MainViewModel(dashboard, tools, profiles, settings);

            var window = new MainWindow { DataContext = mainViewModel };
            MainWindow = window;
            window.Show();
            themeService.Attach(window);

            var visibility = new WindowVisibilityController(new WpfManagementWindow(window));
            ApplicationExitController? exitController = null;
            var trayIcon = new LauncherTrayIconService(
                () => dispatcher.Invoke(visibility.ShowFromTray),
                profile => _ = dashboard.LaunchAsync(profile),
                () => exitController?.RequestExit(),
                workspace.Configuration.Profiles,
                "NGR Launcher");
            exitController = new ApplicationExitController(
                confirmation,
                cancellations,
                new ManagedCommandShutdown(registry),
                visibility,
                () => trayIcon.Unregister(),
                Shutdown);

            _windowVisibility = visibility;
            _trayIcon = trayIcon;
            _exitController = exitController;
            window.Closing += OnMainWindowClosing;
            workspace.Changed += (_, _) => dispatcher.Invoke(
                () => trayIcon.UpdateProfiles(workspace.Configuration.Profiles));

            if (!trayIcon.Register())
            {
                MessageBox.Show(
                    "The system tray icon could not be registered. Closing the window will ask for confirmation and exit NGR Launcher instead.",
                    "NGR Launcher tray",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (loadResult.Warnings.Count > 0)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, loadResult.Warnings),
                    "NGR Launcher configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "NGR Launcher failed to start",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Unregister();
        base.OnExit(e);
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_windowVisibility is null || _windowVisibility.IsCloseAllowed)
        {
            return;
        }

        if (_trayIcon?.IsRegistered == true)
        {
            e.Cancel = _windowVisibility.HandleCloseRequest();
            return;
        }

        e.Cancel = true;
        _ = Dispatcher.BeginInvoke(() => _exitController?.RequestExit());
    }
}
