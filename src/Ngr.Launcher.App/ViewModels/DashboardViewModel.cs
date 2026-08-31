using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ngr.Launcher.App.Services;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Management;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.App.ViewModels;

public sealed class DashboardStepViewModel : ObservableObject
{
    private string _status = "Pending";
    private string? _error;

    public required string ToolId { get; init; }
    public required string ToolName { get; init; }
    public int DelayBeforeSeconds { get; init; }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string? Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }
}

public sealed class DashboardSessionViewModel : ObservableObject
{
    private string _status = "Pending";
    private bool _canCancel = true;
    private Guid? _runnerSessionId;

    public DashboardSessionViewModel(
        Guid id,
        ProfileDefinition profile,
        IEnumerable<ToolDefinition> tools)
    {
        Id = id;
        ProfileName = profile.Name;
        var names = tools.ToDictionary(tool => tool.Id, tool => tool.Name, StringComparer.Ordinal);
        foreach (var step in profile.Steps)
        {
            Steps.Add(new DashboardStepViewModel
            {
                ToolId = step.ToolId,
                ToolName = names.GetValueOrDefault(step.ToolId, step.ToolId),
                DelayBeforeSeconds = step.DelayBeforeSeconds
            });
        }
    }

    public Guid Id { get; }

    public Guid? RunnerSessionId
    {
        get => _runnerSessionId;
        private set => SetProperty(ref _runnerSessionId, value);
    }

    public string ProfileName { get; }
    public ObservableCollection<DashboardStepViewModel> Steps { get; } = [];

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool CanCancel
    {
        get => _canCancel;
        private set => SetProperty(ref _canCancel, value);
    }

    public void ReportProgress(ProfileProgress progress)
    {
        RunnerSessionId ??= progress.SessionId;
        if (progress.Index > 1 && progress.Index - 2 < Steps.Count)
        {
            var previous = Steps[progress.Index - 2];
            if (previous.Status == "Current")
            {
                previous.Status = "Started";
            }
        }

        if (progress.Index - 1 < Steps.Count)
        {
            Steps[progress.Index - 1].Status = "Current";
        }

        Status = $"Running {progress.Index}/{progress.Total}";
    }

    public void MarkCancelling()
    {
        if (CanCancel)
        {
            Status = "Cancelling pending steps…";
        }
    }

    public void ApplyResult(ProfileRunResult result)
    {
        RunnerSessionId = result.SessionId;
        for (var index = 0; index < result.Steps.Count && index < Steps.Count; index++)
        {
            Steps[index].Status = result.Steps[index].Status.ToString();
            Steps[index].Error = result.Steps[index].Error;
        }

        CanCancel = false;
        Status = result.Steps.Any(step => step.Status == StepRunStatus.Failed)
            ? "Completed with failures"
            : result.Steps.Any(step => step.Status == StepRunStatus.Cancelled)
                ? "Cancelled"
                : "Completed";
    }

    public void MarkFailed(Exception exception)
    {
        CanCancel = false;
        Status = $"Failed: {exception.Message}";
    }
}

public sealed class DashboardViewModel : ObservableObject
{
    private readonly LauncherWorkspace _workspace;
    private readonly ProfileRunner _runner;
    private readonly ProfileCancellationRegistry _cancellations;
    private readonly IUiDispatcher _dispatcher;
    private ProfileDefinition? _selectedProfile;
    private DashboardSessionViewModel? _selectedSession;
    private string _statusMessage = string.Empty;

    public DashboardViewModel(
        LauncherWorkspace workspace,
        ProfileRunner runner,
        ProfileCancellationRegistry cancellations,
        IUiDispatcher dispatcher)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _cancellations = cancellations ?? throw new ArgumentNullException(nameof(cancellations));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        LaunchCommand = new RelayCommand(LaunchSelected, () => SelectedProfile is not null);
        CancelCommand = new RelayCommand(CancelSelected, () => SelectedSession?.CanCancel == true);
        _workspace.Changed += (_, _) => _dispatcher.Invoke(RefreshProfiles);
        RefreshProfiles();
    }

    public ObservableCollection<ProfileDefinition> Profiles { get; } = [];
    public ObservableCollection<DashboardSessionViewModel> Sessions { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;
    public bool HasNoProfiles => !HasProfiles;
    public bool HasSessions => Sessions.Count > 0;
    public bool HasNoSessions => !HasSessions;

    public ProfileDefinition? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            LaunchCommand.NotifyCanExecuteChanged();
        }
    }

    public DashboardSessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (!SetProperty(ref _selectedSession, value))
            {
                return;
            }

            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand LaunchCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public void LaunchSelected()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = "Create or select a profile first.";
            return;
        }

        _ = LaunchAsync(SelectedProfile);
    }

    public async Task LaunchAsync(ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var toolSnapshot = _workspace.Configuration.Tools.ToArray();
        var profileSnapshot = profile with
        {
            Steps = profile.Steps.Select(step => step with { }).ToArray()
        };
        var (id, token) = _cancellations.CreateSession();
        var session = new DashboardSessionViewModel(id, profileSnapshot, toolSnapshot);
        _dispatcher.Invoke(() =>
        {
            Sessions.Insert(0, session);
            SelectedSession = session;
            OnPropertyChanged(nameof(HasSessions));
            OnPropertyChanged(nameof(HasNoSessions));
            StatusMessage = $"Started {profileSnapshot.Name}";
        });

        try
        {
            var result = await _runner.RunAsync(
                profileSnapshot,
                toolSnapshot,
                progress => _dispatcher.Invoke(() => session.ReportProgress(progress)),
                token);
            _dispatcher.Invoke(() =>
            {
                session.ApplyResult(result);
                CancelCommand.NotifyCanExecuteChanged();
                StatusMessage = $"{profileSnapshot.Name}: {session.Status}";
            });
        }
        catch (Exception exception)
        {
            _dispatcher.Invoke(() =>
            {
                session.MarkFailed(exception);
                CancelCommand.NotifyCanExecuteChanged();
                StatusMessage = session.Status;
            });
        }
        finally
        {
            _cancellations.Complete(id);
        }
    }

    public void CancelSelected()
    {
        if (SelectedSession is null || !SelectedSession.CanCancel)
        {
            return;
        }

        if (_cancellations.Cancel(SelectedSession.Id))
        {
            SelectedSession.MarkCancelling();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private void RefreshProfiles()
    {
        var selectedId = SelectedProfile?.Id;
        Profiles.Clear();
        foreach (var profile in _workspace.Configuration.Profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = selectedId is null
            ? Profiles.FirstOrDefault()
            : Profiles.FirstOrDefault(profile => string.Equals(profile.Id, selectedId, StringComparison.Ordinal))
                ?? Profiles.FirstOrDefault();

        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
    }
}
