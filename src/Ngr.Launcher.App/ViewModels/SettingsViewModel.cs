using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;

namespace Ngr.Launcher.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly LauncherWorkspace _workspace;
    private readonly IUiDispatcher _dispatcher;
    private readonly IThemeService _themeService;
    private readonly IStartupRegistrationService _startupRegistration;
    private ThemePreference _theme;
    private bool _startWithWindows;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        LauncherWorkspace workspace,
        IUiDispatcher dispatcher,
        IThemeService themeService,
        IStartupRegistrationService startupRegistration)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _startupRegistration = startupRegistration ?? throw new ArgumentNullException(nameof(startupRegistration));
        Themes = Enum.GetValues<ThemePreference>();
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        _workspace.Changed += (_, _) => _dispatcher.Invoke(RefreshFromWorkspace);
        RefreshFromWorkspace();
    }

    public IReadOnlyList<ThemePreference> Themes { get; }

    public ThemePreference Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public async Task SaveAsync()
    {
        bool previousStartupState;
        try
        {
            previousStartupState = _startupRegistration.IsEnabled();
            _startupRegistration.SetEnabled(StartWithWindows);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not update Windows startup: {exception.Message}";
            return;
        }

        var settings = _workspace.Configuration.Settings with
        {
            Theme = Theme,
            StartWithWindows = StartWithWindows,
            StartupPromptAnswered = true
        };

        try
        {
            await _workspace.UpdateSettingsAsync(settings);
        }
        catch (Exception exception)
        {
            TryRestoreStartup(previousStartupState);
            StatusMessage = $"Could not save settings: {exception.Message}";
            return;
        }

        try
        {
            _themeService.Apply(settings.Theme);
            StatusMessage = settings.StartWithWindows
                ? "Settings saved. NGR will start hidden in the tray when you sign in to Windows."
                : "Settings saved. Windows startup is disabled.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Settings were saved, but the theme could not be applied: {exception.Message}";
        }
    }

    private void RefreshFromWorkspace()
    {
        var settings = _workspace.Configuration.Settings;
        Theme = settings.Theme;

        try
        {
            StartWithWindows = _startupRegistration.IsEnabled();
        }
        catch
        {
            StartWithWindows = settings.StartWithWindows;
        }
    }

    private void TryRestoreStartup(bool enabled)
    {
        try
        {
            _startupRegistration.SetEnabled(enabled);
        }
        catch
        {
            // Keep the persistence error as the primary status message.
        }
    }
}
