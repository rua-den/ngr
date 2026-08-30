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
}
