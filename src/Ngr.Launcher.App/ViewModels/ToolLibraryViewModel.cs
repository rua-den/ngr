using System.Collections.ObjectModel;
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

    public ToolLibraryViewModel(
        LauncherWorkspace workspace,
        ICommandSpawner spawner,
        IConfirmationService confirmation,
        IUiDispatcher dispatcher)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        NewCommand = new RelayCommand(New);
        ApplyTemplateCommand = new RelayCommand(ApplySelectedTemplate);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        LaunchCommand = new RelayCommand(LaunchCurrent);

        SelectedTemplate = Templates.FirstOrDefault();
        _workspace.Changed += (_, _) => _dispatcher.Invoke(RefreshFromWorkspace);
        RefreshFromWorkspace();
        New();
    }

    public ObservableCollection<ToolDefinition> Tools { get; } = [];

    public IReadOnlyList<ToolTemplate> Templates => ToolTemplates.All;

    public IReadOnlyList<ToolKind> ToolKinds { get; } = Enum.GetValues<ToolKind>();

    public IReadOnlyList<ShellKind> ShellKinds { get; } = Enum.GetValues<ShellKind>();

    public IReadOnlyList<CommandWindowMode> WindowModes { get; } = Enum.GetValues<CommandWindowMode>();

    public ToolDefinition? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value) && value is not null)
            {
                LoadEditor(value, value.Id);
            }
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
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ToolKind Kind
    {
        get => _kind;
        set => SetProperty(ref _kind, value);
    }

    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public string CommandText
    {
        get => _commandText;
        set => SetProperty(ref _commandText, value);
    }

    public ShellKind Shell
    {
        get => _shell;
        set => SetProperty(ref _shell, value);
    }

    public CommandWindowMode WindowMode
    {
        get => _windowMode;
        set => SetProperty(ref _windowMode, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    public string EnvironmentText
    {
        get => _environmentText;
        set => SetProperty(ref _environmentText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand NewCommand { get; }

    public IRelayCommand ApplyTemplateCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public IRelayCommand LaunchCommand { get; }

    public void New()
    {
        SelectedTool = null;
        LoadEditor(new ToolDefinition { Kind = ToolKind.Application }, originalId: null);
        StatusMessage = "New tool";
    }

    public void ApplySelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        SelectedTool = null;
        LoadEditor(ToolTemplates.Create(SelectedTemplate.Key), originalId: null);
        StatusMessage = $"Template applied: {SelectedTemplate.Name}";
    }

    public async Task SaveAsync()
    {
        try
        {
            var definition = BuildEditorDefinition();
            await _workspace.SaveToolAsync(_originalId, definition);
            _originalId = definition.Id;
            RefreshFromWorkspace(definition.Id);
            StatusMessage = "Tool saved";
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
            StatusMessage = "Select a saved tool first";
            return;
        }

        if (!_confirmation.Confirm("Delete tool", $"Delete tool '{id}'?"))
        {
            return;
        }

        try
        {
            await _workspace.RemoveToolAsync(id);
            New();
            StatusMessage = "Tool deleted";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    public void LaunchCurrent()
    {
        try
        {
            var definition = BuildEditorDefinition();
            var errors = ToolValidator.Validate(definition);
            if (errors.Count > 0)
            {
                throw new System.IO.InvalidDataException(
                    $"Tool is invalid: {string.Join(", ", errors)}.");
            }

            _spawner.Start(definition);
            StatusMessage = $"Launched {definition.Name}";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
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

    private void LoadEditor(ToolDefinition tool, string? originalId)
    {
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
    }

    private void RefreshFromWorkspace(string? selectId = null)
    {
        var id = selectId ?? _originalId ?? SelectedTool?.Id;
        Tools.Clear();
        foreach (var tool in _workspace.Configuration.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            Tools.Add(tool);
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            SelectedTool = Tools.FirstOrDefault(tool => string.Equals(tool.Id, id, StringComparison.Ordinal));
        }
    }

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
