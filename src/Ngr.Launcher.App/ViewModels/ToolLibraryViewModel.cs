using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.App.ViewModels;

public sealed class ToolLibraryViewModel : ObservableObject
{
    private readonly LauncherWorkspace _workspace;
    private readonly ICommandSpawner _spawner;
    private readonly IConfirmationService _confirmation;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPathPickerService _pathPicker;
    private ToolDefinition? _selectedTool;
    private ToolTemplate? _selectedTemplate;
    private string? _originalId;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private ToolKind _kind = ToolKind.Application;
    private string _target = string.Empty;
    private string _arguments = string.Empty;
    private string _commandText = string.Empty;
    private ShellKind _shell = ShellKind.CommandPrompt;
    private CommandWindowMode _windowMode = CommandWindowMode.Hidden;
    private string _workingDirectory = string.Empty;
    private string _environmentText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _validationMessage = string.Empty;
    private bool _canSave;
    private bool _canRun;
    private bool _canDelete;
    private bool _isDirty;
    private bool _autoId = true;
    private bool _loading;

    public ToolLibraryViewModel(
        LauncherWorkspace workspace,
        ICommandSpawner spawner,
        IConfirmationService confirmation,
        IUiDispatcher dispatcher,
        IPathPickerService pathPicker)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _pathPicker = pathPicker ?? throw new ArgumentNullException(nameof(pathPicker));

        NewCommand = new RelayCommand(New);
        ApplyTemplateCommand = new RelayCommand(ApplySelectedTemplate);
        BrowseTargetCommand = new RelayCommand(BrowseTarget);
        BrowseWorkingDirectoryCommand = new RelayCommand(BrowseWorkingDirectory);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        LaunchCommand = new RelayCommand(LaunchCurrent);

        SelectedTemplate = Templates.FirstOrDefault();
        _workspace.Changed += (_, _) => _dispatcher.Invoke(() => RefreshFromWorkspace());
        RefreshFromWorkspace();
        StartNewEditor();
    }

    public ObservableCollection<ToolDefinition> Tools { get; } = [];

    public IReadOnlyList<ToolTemplate> Templates => ToolTemplates.All;
    public IReadOnlyList<ToolKind> ToolKinds { get; } = Enum.GetValues<ToolKind>();
    public IReadOnlyList<ShellKind> ShellKinds { get; } = Enum.GetValues<ShellKind>();
    public IReadOnlyList<CommandWindowMode> WindowModes { get; } = Enum.GetValues<CommandWindowMode>();

    public bool HasTools => Tools.Count > 0;
    public bool HasNoTools => !HasTools;

    public ToolDefinition? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (ReferenceEquals(_selectedTool, value))
            {
                return;
            }

            if (!_loading && IsDirty && !ConfirmDiscardChanges())
            {
                OnPropertyChanged(nameof(SelectedTool));
                return;
            }

            SelectToolWithoutPrompt(value, loadEditor: value is not null);
        }
    }

    public ToolTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
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

    public ToolKind Kind
    {
        get => _kind;
        set
        {
            if (!SetProperty(ref _kind, value))
            {
                return;
            }

            MarkDirty();
            OnPropertyChanged(nameof(IsApplication));
            OnPropertyChanged(nameof(IsCommand));
            RefreshValidation();
        }
    }

    public bool IsApplication => Kind == ToolKind.Application;
    public bool IsCommand => Kind == ToolKind.Command;

    public string Target
    {
        get => _target;
        set
        {
            if (SetProperty(ref _target, value))
            {
                MarkDirty();
                RefreshValidation();
            }
        }
    }

    public string Arguments
    {
        get => _arguments;
        set
        {
            if (SetProperty(ref _arguments, value))
            {
                MarkDirty();
                RefreshValidation();
            }
        }
    }

    public string CommandText
    {
        get => _commandText;
        set
        {
            if (SetProperty(ref _commandText, value))
            {
                MarkDirty();
                RefreshValidation();
            }
        }
    }

    public ShellKind Shell
    {
        get => _shell;
        set
        {
            if (SetProperty(ref _shell, value))
            {
                MarkDirty();
                RefreshValidation();
            }
        }
    }

    public CommandWindowMode WindowMode
    {
        get => _windowMode;
        set
        {
            if (SetProperty(ref _windowMode, value))
            {
                MarkDirty();
                RefreshValidation();
            }
        }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (SetProperty(ref _workingDirectory, value))
            {
                MarkDirty();
                RefreshValidation();
            }
        }
    }

    public string EnvironmentText
    {
        get => _environmentText;
        set
        {
            if (SetProperty(ref _environmentText, value))
            {
                MarkDirty();
                RefreshValidation();
            }
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

    public bool CanRun
    {
        get => _canRun;
        private set => SetProperty(ref _canRun, value);
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
    public IRelayCommand ApplyTemplateCommand { get; }
    public IRelayCommand BrowseTargetCommand { get; }
    public IRelayCommand BrowseWorkingDirectoryCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IRelayCommand LaunchCommand { get; }

    public void New()
    {
        if (IsDirty && !ConfirmDiscardChanges())
        {
            return;
        }

        StartNewEditor();
    }

    public void ApplySelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        if (IsDirty && !ConfirmDiscardChanges())
        {
            return;
        }

        SelectToolWithoutPrompt(null, loadEditor: false);
        var template = ToolTemplates.Create(SelectedTemplate.Key);
        LoadEditor(template, originalId: null, autoId: true);
        SetGeneratedId(GenerateUniqueId(CreateId(template.Id.Length > 0 ? template.Id : template.Name)));
        IsDirty = true;
        RefreshValidation();
        StatusMessage = $"Template applied: {SelectedTemplate.Name}. Review it, then Save or Test run.";
    }

    public void BrowseTarget()
    {
        var selected = _pathPicker.PickApplicationTarget(Target);
        if (selected is null || string.IsNullOrWhiteSpace(selected.Target))
        {
            return;
        }

        Target = selected.Target;
        if (string.IsNullOrWhiteSpace(Name) || string.Equals(Name, "Application", StringComparison.OrdinalIgnoreCase))
        {
            Name = selected.DisplayName;
        }

        StatusMessage = $"Selected '{selected.DisplayName}'. Test it before saving if you want to verify the launch.";
    }

    public void BrowseWorkingDirectory()
    {
        var selected = _pathPicker.PickFolder(WorkingDirectory);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        WorkingDirectory = selected;
        StatusMessage = "Working directory selected.";
    }

    public async Task SaveAsync()
    {
        RefreshValidation();
        if (!CanSave)
        {
            StatusMessage = IsDirty
                ? "Fix the configuration issue before saving."
                : "No unsaved changes.";
            return;
        }

        try
        {
            var definition = BuildEditorDefinition();
            await _workspace.SaveToolAsync(_originalId, definition);
            _originalId = definition.Id;
            _autoId = false;
            IsDirty = false;
            RefreshFromWorkspace(definition.Id);
            RefreshValidation();
            StatusMessage = $"Saved '{definition.Name}'.";
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
            StatusMessage = "Select a saved tool first.";
            return;
        }

        if (!_confirmation.Confirm("Delete tool", $"Delete saved tool '{Name}'? Profiles that reference it may need to be fixed."))
        {
            return;
        }

        try
        {
            await _workspace.RemoveToolAsync(id);
            IsDirty = false;
            StartNewEditor();
            StatusMessage = "Tool deleted.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    public void LaunchCurrent()
    {
        RefreshValidation();
        if (!CanRun)
        {
            StatusMessage = "Fix the configuration issue before testing.";
            return;
        }

        try
        {
            var definition = BuildEditorDefinition();
            _spawner.Start(definition);
            StatusMessage = $"Started '{definition.Name}'.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not start tool: {exception.Message}";
        }
    }

    private ToolDefinition BuildEditorDefinition()
    {
        var environment = Kind == ToolKind.Command
            ? ParseEnvironment(EnvironmentText)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new ToolDefinition
        {
            Id = Id.Trim(),
            Name = Name.Trim(),
            Kind = Kind,
            Target = Kind == ToolKind.Application ? NullIfWhiteSpace(Target) : null,
            Arguments = Kind == ToolKind.Application ? NullIfWhiteSpace(Arguments) : null,
            CommandText = Kind == ToolKind.Command ? NullIfWhiteSpace(CommandText) : null,
            Shell = Kind == ToolKind.Command ? Shell : null,
            WindowMode = Kind == ToolKind.Command ? WindowMode : null,
            WorkingDirectory = NullIfWhiteSpace(WorkingDirectory),
            EnvironmentVariables = environment
        };
    }

    private void StartNewEditor()
    {
        SelectToolWithoutPrompt(null, loadEditor: false);
        LoadEditor(new ToolDefinition { Kind = ToolKind.Application }, originalId: null, autoId: true);
        StatusMessage = "New tool — choose an installed application or switch to Command.";
    }

    private void LoadEditor(ToolDefinition tool, string? originalId, bool autoId)
    {
        _loading = true;
        _autoId = autoId;
        _originalId = originalId;
        Id = tool.Id;
        Name = tool.Name;
        Kind = tool.Kind;
        Target = tool.Target ?? string.Empty;
        Arguments = tool.Arguments ?? string.Empty;
        CommandText = tool.CommandText ?? string.Empty;
        Shell = tool.Shell ?? ShellKind.CommandPrompt;
        WindowMode = tool.WindowMode ?? CommandWindowMode.Hidden;
        WorkingDirectory = tool.WorkingDirectory ?? string.Empty;
        EnvironmentText = string.Join(
            Environment.NewLine,
            tool.EnvironmentVariables.Select(pair => $"{pair.Key}={pair.Value}"));
        _loading = false;
        _autoId = autoId;

        if (autoId)
        {
            var seed = !string.IsNullOrWhiteSpace(tool.Id) ? tool.Id : tool.Name;
            SetGeneratedId(string.IsNullOrWhiteSpace(seed) ? string.Empty : GenerateUniqueId(CreateId(seed)));
        }

        IsDirty = false;
        CanDelete = !string.IsNullOrWhiteSpace(_originalId);
        OnPropertyChanged(nameof(IsApplication));
        OnPropertyChanged(nameof(IsCommand));
        RefreshValidation();
    }

    private void RefreshFromWorkspace(string? selectId = null)
    {
        var id = selectId ?? _originalId ?? _selectedTool?.Id;
        Tools.Clear();
        foreach (var tool in _workspace.Configuration.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            Tools.Add(tool);
        }

        OnPropertyChanged(nameof(HasTools));
        OnPropertyChanged(nameof(HasNoTools));

        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var matching = Tools.FirstOrDefault(tool => string.Equals(tool.Id, id, StringComparison.Ordinal));
        if (IsDirty)
        {
            SetProperty(ref _selectedTool, matching, nameof(SelectedTool));
            CanDelete = !string.IsNullOrWhiteSpace(_originalId) && matching is not null;
            return;
        }

        SelectToolWithoutPrompt(matching, loadEditor: matching is not null);
    }

    private void SelectToolWithoutPrompt(ToolDefinition? tool, bool loadEditor)
    {
        if (!SetProperty(ref _selectedTool, tool, nameof(SelectedTool)))
        {
            return;
        }

        if (loadEditor && tool is not null)
        {
            LoadEditor(tool, tool.Id, autoId: false);
        }
        else if (tool is null)
        {
            CanDelete = false;
        }
    }

    private bool ConfirmDiscardChanges() =>
        _confirmation.Confirm(
            "Discard unsaved tool changes?",
            "This tool has changes that have not been saved. Discard them and continue?");

    private void MarkDirty()
    {
        if (!_loading)
        {
            IsDirty = true;
        }
    }

    private void RefreshValidation()
    {
        if (_loading)
        {
            return;
        }

        var errors = new List<string>();
        try
        {
            var definition = BuildEditorDefinition();
            errors.AddRange(ToolValidator.Validate(definition).Select(ToMessage));

            if (definition.Kind == ToolKind.Application
                && !string.IsNullOrWhiteSpace(definition.Target)
                && LooksLikeFileSystemPath(definition.Target)
                && !File.Exists(definition.Target))
            {
                errors.Add("The selected target file does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(definition.WorkingDirectory)
                && !Directory.Exists(definition.WorkingDirectory))
            {
                errors.Add("The working directory does not exist.");
            }
        }
        catch (System.IO.InvalidDataException exception)
        {
            errors.Add(exception.Message);
        }

        var distinct = errors.Distinct(StringComparer.Ordinal).ToArray();
        var valid = distinct.Length == 0;
        CanSave = valid && IsDirty;
        CanRun = valid;
        ValidationMessage = valid
            ? IsDirty
                ? "Ready — save the changes, or test run first."
                : "Saved configuration is valid and ready to run."
            : string.Join(Environment.NewLine, distinct);
    }

    private void SetGeneratedId(string value) =>
        SetProperty(ref _id, value, nameof(Id));

    private string GenerateUniqueId(string seed)
    {
        var baseId = string.IsNullOrWhiteSpace(seed) ? "tool" : seed;
        var candidate = baseId;
        var suffix = 2;
        while (_workspace.Configuration.Tools.Any(tool =>
                   !string.Equals(tool.Id, _originalId, StringComparison.Ordinal)
                   && string.Equals(tool.Id, candidate, StringComparison.Ordinal)))
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

    private static bool LooksLikeFileSystemPath(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return false;
        }

        return Path.IsPathRooted(target) || target.StartsWith(".\\", StringComparison.Ordinal);
    }

    private static string ToMessage(ToolValidationError error) => error switch
    {
        ToolValidationError.IdRequired => "Enter a name so NGR can generate an ID.",
        ToolValidationError.NameRequired => "Tool name is required.",
        ToolValidationError.KindInvalid => "Choose Application or Command.",
        ToolValidationError.TargetRequired => "Choose an application/file or enter a target URI.",
        ToolValidationError.CommandTextRequired => "Command text is required.",
        ToolValidationError.ShellRequired or ToolValidationError.ShellInvalid => "Choose a valid command shell.",
        ToolValidationError.WindowModeRequired or ToolValidationError.WindowModeInvalid => "Choose how the command window should run.",
        ToolValidationError.EnvironmentVariablesRequired => "Environment variables are invalid.",
        ToolValidationError.EnvironmentKeyInvalid => "Environment variable names may contain only letters, digits and underscores.",
        ToolValidationError.WorkingDirectoryInvalid => "Working directory path is invalid.",
        ToolValidationError.IncompatibleField => "Some fields do not belong to the selected tool type.",
        _ => error.ToString()
    };

    private static IReadOnlyDictionary<string, string> ParseEnvironment(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split(
                     ["\r\n", "\n"],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = rawLine.IndexOf('=');
            if (separator <= 0)
            {
                throw new System.IO.InvalidDataException(
                    $"Environment line '{rawLine}' must use KEY=VALUE format.");
            }

            result[rawLine[..separator].Trim()] = rawLine[(separator + 1)..];
        }

        return result;
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
