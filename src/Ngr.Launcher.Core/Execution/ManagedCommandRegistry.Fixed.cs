namespace Ngr.Launcher.Core.Execution;

public interface IManagedProcess
{
    event EventHandler? Exited;

    void KillTree();
}

public interface IProfileSessionCoordinator
{
    void CancelAll();
}

public sealed class ManagedCommandRegistry(IProfileSessionCoordinator? sessionCoordinator = null)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<IManagedProcess>> _live = new(StringComparer.Ordinal);
    private readonly IProfileSessionCoordinator? _sessionCoordinator = sessionCoordinator;
    private bool _isStopped;

    public void Register(string toolId, IManagedProcess process)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(process);

        lock (_gate)
        {
            if (_isStopped)
            {
                SafeKill(process);
                return;
            }

            if (!_live.TryGetValue(toolId, out var instances))
            {
                instances = [];
                _live.Add(toolId, instances);
            }

            instances.Add(process);
            process.Exited += HandleExit;
        }

        void HandleExit(object? sender, EventArgs eventArgs)
        {
            process.Exited -= HandleExit;
            Remove(toolId, process);
        }
    }

    public IReadOnlyList<IManagedProcess> Live(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        lock (_gate)
        {
            return _live.TryGetValue(toolId, out var instances)
                ? instances.ToArray()
                : Array.Empty<IManagedProcess>();
        }
    }

    public void RegisterApplication(string toolId, IManagedProcess process)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(process);
        // Desktop applications are launched but deliberately not lifecycle-managed.
    }

    public void StopAll()
    {
        try
        {
            _sessionCoordinator?.CancelAll();
        }
        catch
        {
            // Command shutdown remains best-effort even if session cancellation reports an error.
        }

        IManagedProcess[] snapshot;
        lock (_gate)
        {
            _isStopped = true;
            snapshot = _live.Values.SelectMany(instances => instances).ToArray();
            _live.Clear();
        }

        foreach (var process in snapshot)
        {
            SafeKill(process);
        }
    }

    private static void SafeKill(IManagedProcess process)
    {
        try
        {
            process.KillTree();
        }
        catch
        {
            // One process must not prevent the remaining process trees from stopping.
        }
    }

    private void Remove(string toolId, IManagedProcess process)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(toolId, out var instances))
            {
                return;
            }

            instances.Remove(process);
            if (instances.Count == 0)
            {
                _live.Remove(toolId);
            }
        }
    }
}
