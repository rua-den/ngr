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
    private ThemePreference _theme;
    private bool _startWithWindows;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        LauncherWorkspace workspace,
        IUiDispatcher dispatcher,
        IThemeService themeService)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
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
        try
        {
            var settings = _workspace.Configuration.Settings with
            {
                Theme = Theme,
                StartWithWindows = StartWithWindows,
                StartupPromptAnswered = true
            };
            await _workspace.UpdateSettingsAsync(settings);
            _themeService.Apply(settings.Theme);
            StatusMessage = "Settings saved";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void RefreshFromWorkspace()
    {
        var settings = _workspace.Configuration.Settings;
        Theme = settings.Theme;
        StartWithWindows = settings.StartWithWindows;
    }
}
