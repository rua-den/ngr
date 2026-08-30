using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.App.ViewModels;

public sealed class ProfileStepEditorViewModel : ObservableObject
{
    private string _toolId = string.Empty;
    private int _delayBeforeSeconds;

    public string ToolId
    {
        get => _toolId;
        set => SetProperty(ref _toolId, value);
    }

    public int DelayBeforeSeconds
    {
        get => _delayBeforeSeconds;
        set => SetProperty(ref _delayBeforeSeconds, value);
    }
}

public sealed class ProfilesViewModel : ObservableObject
{
    private readonly LauncherWorkspace _workspace;
    private readonly IConfirmationService _confirmation;
    private readonly IUiDispatcher _dispatcher;
    private ProfileDefinition? _selectedProfile;
    private ProfileStepEditorViewModel? _selectedStep;
    private string? _originalId;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _statusMessage = string.Empty;

    public ProfilesViewModel(
        LauncherWorkspace workspace,
        IConfirmationService confirmation,
        IUiDispatcher dispatcher)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        NewCommand = new RelayCommand(New);
        AddStepCommand = new RelayCommand(AddStep);
        RemoveStepCommand = new RelayCommand(RemoveSelectedStep);
        MoveStepUpCommand = new RelayCommand(MoveSelectedStepUp);
        MoveStepDownCommand = new RelayCommand(MoveSelectedStepDown);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);

        _workspace.Changed += (_, _) => _dispatcher.Invoke(() => RefreshFromWorkspace());
        RefreshFromWorkspace();
        New();
    }

    public ObservableCollection<ProfileDefinition> Profiles { get; } = [];

    public ObservableCollection<ToolDefinition> AvailableTools { get; } = [];

    public ObservableCollection<ProfileStepEditorViewModel> Steps { get; } = [];

    public ProfileDefinition? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                LoadEditor(value, value.Id);
            }
        }
    }

    public ProfileStepEditorViewModel? SelectedStep
    {
        get => _selectedStep;
        set => SetProperty(ref _selectedStep, value);
    }

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand NewCommand { get; }

    public IRelayCommand AddStepCommand { get; }

    public IRelayCommand RemoveStepCommand { get; }

    public IRelayCommand MoveStepUpCommand { get; }

    public IRelayCommand MoveStepDownCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public void New()
    {
        SelectedProfile = null;
        _originalId = null;
        Id = string.Empty;
        Name = string.Empty;
        Steps.Clear();
        SelectedStep = null;
        StatusMessage = "New profile";
    }

    public void AddStep()
    {
        var firstTool = AvailableTools.FirstOrDefault();
        if (firstTool is null)
        {
            StatusMessage = "Create a tool before adding profile steps";
            return;
        }

        var step = new ProfileStepEditorViewModel { ToolId = firstTool.Id };
        Steps.Add(step);
        SelectedStep = step;
    }

    public void RemoveSelectedStep()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        Steps.Remove(SelectedStep);
        SelectedStep = Steps.Count == 0
            ? null
            : Steps[Math.Clamp(index, 0, Steps.Count - 1)];
    }

    public void MoveSelectedStepUp()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index <= 0)
        {
            return;
        }

        Steps.Move(index, index - 1);
    }

    public void MoveSelectedStepDown()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index < 0 || index >= Steps.Count - 1)
        {
            return;
        }

        Steps.Move(index, index + 1);
    }

    public async Task SaveAsync()
    {
        try
        {
            var profile = new ProfileDefinition
            {
                Id = Id.Trim(),
                Name = Name.Trim(),
                Steps = Steps.Select(step => new ProfileStep
                {
                    ToolId = step.ToolId,
                    DelayBeforeSeconds = step.DelayBeforeSeconds
                }).ToArray()
            };

            await _workspace.SaveProfileAsync(_originalId, profile);
            _originalId = profile.Id;
            RefreshFromWorkspace(profile.Id);
            StatusMessage = "Profile saved";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    public async Task DeleteAsync()
    {
        var id = _originalId;
        if (string.IsNullOrWhiteSpace(id))
        {
            StatusMessage = "Select a saved profile first";
            return;
        }

        if (!_confirmation.Confirm("Delete profile", $"Delete profile '{id}'?"))
        {
            return;
        }

        try
        {
            await _workspace.RemoveProfileAsync(id);
            New();
            StatusMessage = "Profile deleted";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void LoadEditor(ProfileDefinition profile, string originalId)
    {
        _originalId = originalId;
        Id = profile.Id;
        Name = profile.Name;
        Steps.Clear();
        foreach (var step in profile.Steps)
        {
            Steps.Add(new ProfileStepEditorViewModel
            {
                ToolId = step.ToolId,
                DelayBeforeSeconds = step.DelayBeforeSeconds
            });
        }

        SelectedStep = Steps.FirstOrDefault();
    }

    private void RefreshFromWorkspace(string? selectId = null)
    {
        var profileId = selectId ?? _originalId ?? SelectedProfile?.Id;

        AvailableTools.Clear();
        foreach (var tool in _workspace.Configuration.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            AvailableTools.Add(tool);
        }

        Profiles.Clear();
        foreach (var profile in _workspace.Configuration.Profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(profile);
        }

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            SelectedProfile = Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, profileId, StringComparison.Ordinal));
        }
    }
}
