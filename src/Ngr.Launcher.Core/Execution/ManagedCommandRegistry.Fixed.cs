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

    public void Register(string toolId, IManagedProcess process)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(process);

        lock (_gate)
        {
            if (!_live.TryGetValue(toolId, out var instances))
            {
                instances = [];
                _live.Add(toolId, instances);
            }

            instances.Add(process);
        }

        process.Exited += HandleExit;

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
        _sessionCoordinator?.CancelAll();

        IManagedProcess[] snapshot;
        lock (_gate)
        {
            snapshot = _live.Values.SelectMany(instances => instances).ToArray();
        }

        foreach (var process in snapshot)
        {
            try
            {
                process.KillTree();
            }
            catch
            {
                // Best effort: one process must not prevent the remaining trees from stopping.
            }
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
