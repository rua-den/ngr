using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ngr.Launcher.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private object _currentPage;
    private string _currentTitle;

    public MainViewModel(
        DashboardViewModel dashboard,
        ToolLibraryViewModel toolLibrary,
        ProfilesViewModel profiles,
        SettingsViewModel settings)
    {
        Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        ToolLibrary = toolLibrary ?? throw new ArgumentNullException(nameof(toolLibrary));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _currentPage = Dashboard;
        _currentTitle = "Dashboard";
        NavigateCommand = new RelayCommand<string>(Navigate);
    }

    public DashboardViewModel Dashboard { get; }

    public ToolLibraryViewModel ToolLibrary { get; }

    public ProfilesViewModel Profiles { get; }

    public SettingsViewModel Settings { get; }

    public object CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        private set => SetProperty(ref _currentTitle, value);
    }

    public IRelayCommand<string> NavigateCommand { get; }

    private void Navigate(string? destination)
    {
        (CurrentTitle, CurrentPage) = destination switch
        {
            "Dashboard" => ("Dashboard", Dashboard),
            "Tools" => ("Tool Library", ToolLibrary),
            "Profiles" => ("Profiles", Profiles),
            "Settings" => ("Settings", Settings),
            _ => (CurrentTitle, CurrentPage)
        };
    }
}
