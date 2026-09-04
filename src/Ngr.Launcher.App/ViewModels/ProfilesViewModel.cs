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
    private ToolDefinition? _toolToAdd;
    private string? _originalId;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _statusMessage = string.Empty;
    private string _validationMessage = string.Empty;
    private bool _canSave;
    private bool _canDelete;
    private bool _isDirty;
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
        AddStepCommand = new RelayCommand(AddStep, () => ToolToAdd is not null);
        RemoveStepCommand = new RelayCommand(RemoveSelectedStep, () => SelectedStep is not null);
        MoveStepUpCommand = new RelayCommand(MoveSelectedStepUp, CanMoveSelectedStepUp);
        MoveStepDownCommand = new RelayCommand(MoveSelectedStepDown, CanMoveSelectedStepDown);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);

        _workspace.Changed += (_, _) => _dispatcher.Invoke(() => RefreshFromWorkspace());
        RefreshFromWorkspace();
        StartNewEditor();
    }

    public ObservableCollection<ProfileDefinition> Profiles { get; } = [];
    public ObservableCollection<ToolDefinition> AvailableTools { get; } = [];
    public ObservableCollection<ProfileStepEditorViewModel> Steps { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;
    public bool HasNoProfiles => !HasProfiles;
    public bool HasAvailableTools => AvailableTools.Count > 0;
    public bool HasNoAvailableTools => !HasAvailableTools;
    public bool HasSteps => Steps.Count > 0;
    public bool HasNoSteps => !HasSteps;

    public ProfileDefinition? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (ReferenceEquals(_selectedProfile, value))
            {
                return;
            }

            if (!_loading && IsDirty && !ConfirmDiscardChanges())
            {
                OnPropertyChanged(nameof(SelectedProfile));
                return;
            }

            SelectProfileWithoutPrompt(value, loadEditor: value is not null);
        }
    }

    public ProfileStepEditorViewModel? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetProperty(ref _selectedStep, value))
            {
                NotifyStepCommands();
            }
        }
    }

    public ToolDefinition? ToolToAdd
    {
        get => _toolToAdd;
        set
        {
            if (SetProperty(ref _toolToAdd, value))
            {
                AddStepCommand.NotifyCanExecuteChanged();
            }
        }
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
                MarkDirty();
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

            MarkDirty();
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

    public bool CanDelete
    {
        get => _canDelete;
        private set => SetProperty(ref _canDelete, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
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
        if (IsDirty && !ConfirmDiscardChanges())
        {
            return;
        }

        StartNewEditor();
    }

    public void AddStep()
    {
        if (ToolToAdd is null)
        {
            StatusMessage = "Choose a tool to add first.";
            return;
        }

        var step = new ProfileStepEditorViewModel { ToolId = ToolToAdd.Id };
        AttachStep(step);
        Steps.Add(step);
        SelectedStep = step;
        MarkDirty();
        NotifyStepsChanged();
        RefreshValidation();
        StatusMessage = $"Added '{ToolToAdd.Name}'. Set a delay or reorder it if needed.";
    }

    public void RemoveSelectedStep()
    {
        if (SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        DetachStep(SelectedStep);
        Steps.Remove(SelectedStep);
        SelectedStep = Steps.Count == 0
            ? null
            : Steps[Math.Clamp(index, 0, Steps.Count - 1)];
        MarkDirty();
        NotifyStepsChanged();
        RefreshValidation();
        StatusMessage = "Step removed.";
    }

    public void MoveSelectedStepUp()
    {
        if (!CanMoveSelectedStepUp() || SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        Steps.Move(index, index - 1);
        MarkDirty();
        NotifyStepCommands();
        RefreshValidation();
        StatusMessage = "Step moved up.";
    }

    public void MoveSelectedStepDown()
    {
        if (!CanMoveSelectedStepDown() || SelectedStep is null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        Steps.Move(index, index + 1);
        MarkDirty();
        NotifyStepCommands();
        RefreshValidation();
        StatusMessage = "Step moved down.";
    }

    public async Task SaveAsync()
    {
        RefreshValidation();
        if (!CanSave)
        {
            StatusMessage = IsDirty
                ? "Fix the profile configuration issue before saving."
                : "No unsaved changes.";
            return;
        }

        try
        {
            var profile = BuildEditorDefinition();
            await _workspace.SaveProfileAsync(_originalId, profile);
            _originalId = profile.Id;
            _autoId = false;
            IsDirty = false;
            RefreshFromWorkspace(profile.Id);
            RefreshValidation();
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

        if (!_confirmation.Confirm("Delete profile", $"Delete saved profile '{Name}'?"))
        {
            return;
        }

        try
        {
            await _workspace.RemoveProfileAsync(id);
            IsDirty = false;
            StartNewEditor();
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

    private void StartNewEditor()
    {
        SelectProfileWithoutPrompt(null, loadEditor: false);
        _originalId = null;
        _autoId = true;
        _loading = true;
        Id = string.Empty;
        Name = string.Empty;
        ClearSteps();
        SelectedStep = null;
        _loading = false;
        _autoId = true;
        IsDirty = false;
        CanDelete = false;
        NotifyStepsChanged();
        RefreshValidation();
        StatusMessage = HasAvailableTools
            ? "New profile — choose a tool, add it, then build the launch order."
            : "Create at least one tool before building a profile.";
    }

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
        IsDirty = false;
        CanDelete = true;
        NotifyStepsChanged();
        RefreshValidation();
    }

    private void RefreshFromWorkspace(string? selectId = null)
    {
        var profileId = selectId ?? _originalId ?? _selectedProfile?.Id;
        var toolToAddId = ToolToAdd?.Id;

        AvailableTools.Clear();
        foreach (var tool in _workspace.Configuration.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            AvailableTools.Add(tool);
        }

        ToolToAdd = toolToAddId is null
            ? AvailableTools.FirstOrDefault()
            : AvailableTools.FirstOrDefault(tool => string.Equals(tool.Id, toolToAddId, StringComparison.Ordinal))
                ?? AvailableTools.FirstOrDefault();

        Profiles.Clear();
        foreach (var profile in _workspace.Configuration.Profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(profile);
        }

        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(HasAvailableTools));
        OnPropertyChanged(nameof(HasNoAvailableTools));
        AddStepCommand.NotifyCanExecuteChanged();

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            var matching = Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, profileId, StringComparison.Ordinal));
            if (IsDirty)
            {
                SetProperty(ref _selectedProfile, matching, nameof(SelectedProfile));
                CanDelete = !string.IsNullOrWhiteSpace(_originalId) && matching is not null;
            }
            else
            {
                SelectProfileWithoutPrompt(matching, loadEditor: matching is not null);
            }
        }

        RefreshValidation();
    }

    private void SelectProfileWithoutPrompt(ProfileDefinition? profile, bool loadEditor)
    {
        if (!SetProperty(ref _selectedProfile, profile, nameof(SelectedProfile)))
        {
            return;
        }

        if (loadEditor && profile is not null)
        {
            LoadEditor(profile, profile.Id, autoId: false);
        }
        else if (profile is null)
        {
            CanDelete = false;
        }
    }

    private bool ConfirmDiscardChanges() =>
        _confirmation.Confirm(
            "Discard unsaved profile changes?",
            "This profile has changes that have not been saved. Discard them and continue?");

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

        var valid = errors.Length == 0;
        CanSave = valid && IsDirty;
        ValidationMessage = valid
            ? IsDirty
                ? $"Ready — save {Steps.Count} step{(Steps.Count == 1 ? string.Empty : "s")} in this order."
                : $"Saved profile is valid — {Steps.Count} step{(Steps.Count == 1 ? string.Empty : "s")} will run in this order."
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

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkDirty();
        RefreshValidation();
    }

    private void MarkDirty()
    {
        if (!_loading)
        {
            IsDirty = true;
        }
    }

    private void NotifyStepsChanged()
    {
        OnPropertyChanged(nameof(HasSteps));
        OnPropertyChanged(nameof(HasNoSteps));
        NotifyStepCommands();
    }

    private void NotifyStepCommands()
    {
        RemoveStepCommand.NotifyCanExecuteChanged();
        MoveStepUpCommand.NotifyCanExecuteChanged();
        MoveStepDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveSelectedStepUp() =>
        SelectedStep is not null && Steps.IndexOf(SelectedStep) > 0;

    private bool CanMoveSelectedStepDown()
    {
        if (SelectedStep is null)
        {
            return false;
        }

        var index = Steps.IndexOf(SelectedStep);
        return index >= 0 && index < Steps.Count - 1;
    }

    private void SetGeneratedId(string value) =>
        SetProperty(ref _id, value, nameof(Id));

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
