using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Configuration;
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
    private string _validationMessage = string.Empty;
    private bool _canSave;
    private bool _autoId = true;
    private bool _loading;

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
                LoadEditor(value, value.Id, autoId: false);
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
        set
        {
            if (!_loading)
            {
                _autoId = false;
            }

            if (SetProperty(ref _id, value))
            {
                RefreshValidation();
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            if (!_loading && _autoId)
            {
                SetGeneratedId(string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : GenerateUniqueId(CreateId(value)));
            }

            RefreshValidation();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool CanSave
    {
        get => _canSave;
        private set => SetProperty(ref _canSave, value);
    }

    public bool HasAvailableTools => AvailableTools.Count > 0;

    public bool HasNoAvailableTools => AvailableTools.Count == 0;

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
        _autoId = true;
        _loading = true;
        Id = string.Empty;
        Name = string.Empty;
        ClearSteps();
        SelectedStep = null;
        _loading = false;
        _autoId = true;
        RefreshValidation();
        StatusMessage = HasAvailableTools
            ? "New profile — add tools in the order you want them launched."
            : "Create at least one tool before building a profile.";
    }

    public void AddStep()
    {
        var firstTool = AvailableTools.FirstOrDefault();
        if (firstTool is null)
        {
            StatusMessage = "No tools are available. Create a tool in Tool Library first.";
            return;
        }

        var step = new ProfileStepEditorViewModel { ToolId = firstTool.Id };
        AttachStep(step);
        Steps.Add(step);
        SelectedStep = step;
        RefreshValidation();
        StatusMessage = $"Added '{firstTool.Name}'. Choose a different tool or set a delay if needed.";
    }

    public void RemoveSelectedStep()
    {
        if (SelectedStep is null)
        {
            StatusMessage = "Select a profile step first.";
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        DetachStep(SelectedStep);
        Steps.Remove(SelectedStep);
        SelectedStep = Steps.Count == 0
            ? null
            : Steps[Math.Clamp(index, 0, Steps.Count - 1)];
        RefreshValidation();
        StatusMessage = "Step removed.";
    }

    public void MoveSelectedStepUp()
    {
        if (SelectedStep is null)
        {
            StatusMessage = "Select a profile step first.";
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index <= 0)
        {
            return;
        }

        Steps.Move(index, index - 1);
        RefreshValidation();
        StatusMessage = "Step moved up.";
    }

    public void MoveSelectedStepDown()
    {
        if (SelectedStep is null)
        {
            StatusMessage = "Select a profile step first.";
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index < 0 || index >= Steps.Count - 1)
        {
            return;
        }

        Steps.Move(index, index + 1);
        RefreshValidation();
        StatusMessage = "Step moved down.";
    }

    public async Task SaveAsync()
    {
        RefreshValidation();
        if (!CanSave)
        {
            StatusMessage = "Fix the profile configuration issue before saving.";
            return;
        }

        try
        {
            var profile = BuildEditorDefinition();
            await _workspace.SaveProfileAsync(_originalId, profile);
            _originalId = profile.Id;
            _autoId = false;
            RefreshFromWorkspace(profile.Id);
            StatusMessage = $"Saved '{profile.Name}'. It is ready to run from Dashboard or the tray.";
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
            StatusMessage = "Select a saved profile first.";
            return;
        }

        if (!_confirmation.Confirm("Delete profile", $"Delete profile '{Name}'?"))
        {
            return;
        }

        try
        {
            await _workspace.RemoveProfileAsync(id);
            New();
            StatusMessage = "Profile deleted.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private ProfileDefinition BuildEditorDefinition() => new()
    {
        Id = Id.Trim(),
        Name = Name.Trim(),
        Steps = Steps.Select(step => new ProfileStep
        {
            ToolId = step.ToolId,
            DelayBeforeSeconds = step.DelayBeforeSeconds
        }).ToArray()
    };

    private void LoadEditor(ProfileDefinition profile, string originalId, bool autoId)
    {
        _loading = true;
        _autoId = autoId;
        _originalId = originalId;
        Id = profile.Id;
        Name = profile.Name;
        ClearSteps();
        foreach (var source in profile.Steps)
        {
            var step = new ProfileStepEditorViewModel
            {
                ToolId = source.ToolId,
                DelayBeforeSeconds = source.DelayBeforeSeconds
            };
            AttachStep(step);
            Steps.Add(step);
        }

        SelectedStep = Steps.FirstOrDefault();
        _loading = false;
        _autoId = autoId;
        RefreshValidation();
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

        OnPropertyChanged(nameof(HasAvailableTools));
        OnPropertyChanged(nameof(HasNoAvailableTools));

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            SelectedProfile = Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, profileId, StringComparison.Ordinal));
        }

        RefreshValidation();
    }

    private void RefreshValidation()
    {
        if (_loading)
        {
            return;
        }

        var errors = ProfileValidator.Validate(BuildEditorDefinition(), AvailableTools)
            .Select(ToMessage)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        CanSave = errors.Length == 0;
        ValidationMessage = errors.Length == 0
            ? $"Ready — {Steps.Count} step{(Steps.Count == 1 ? string.Empty : "s")} will run in this order."
            : string.Join(Environment.NewLine, errors);
    }

    private void AttachStep(ProfileStepEditorViewModel step) => step.PropertyChanged += OnStepPropertyChanged;

    private void DetachStep(ProfileStepEditorViewModel step) => step.PropertyChanged -= OnStepPropertyChanged;

    private void ClearSteps()
    {
        foreach (var step in Steps)
        {
            DetachStep(step);
        }

        Steps.Clear();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshValidation();

    private void SetGeneratedId(string value)
    {
        if (SetProperty(ref _id, value, nameof(Id)))
        {
            RefreshValidation();
        }
    }

    private string GenerateUniqueId(string seed)
    {
        var baseId = string.IsNullOrWhiteSpace(seed) ? "profile" : seed;
        var candidate = baseId;
        var suffix = 2;
        while (_workspace.Configuration.Profiles.Any(profile =>
                   !string.Equals(profile.Id, _originalId, StringComparison.Ordinal)
                   && string.Equals(profile.Id, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{baseId}-{suffix++}";
        }

        return candidate;
    }

    private static string CreateId(string text)
    {
        var builder = new StringBuilder();
        var separatorPending = false;
        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                separatorPending = false;
            }
            else if (builder.Length > 0)
            {
                separatorPending = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string ToMessage(ProfileValidationError error) => error switch
    {
        ProfileValidationError.IdRequired => "Enter a name so NGR can generate a profile ID.",
        ProfileValidationError.NameRequired => "Profile name is required.",
        ProfileValidationError.StepsRequired => "Add at least one tool to this profile.",
        ProfileValidationError.StepRequired => "A profile step is invalid.",
        ProfileValidationError.ToolIdRequired => "Every step must select a tool.",
        ProfileValidationError.ToolNotFound => "One of the selected tools no longer exists.",
        ProfileValidationError.DelayOutOfRange => "Delay must be between 0 and 300 seconds.",
        _ => error.ToString()
    };
}
