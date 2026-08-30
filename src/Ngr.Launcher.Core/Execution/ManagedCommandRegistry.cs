namespace Ngr.Launcher.Core.Execution;

public interface IManagedProcess
{
    event EventHandler? Exited;
    void KillTree();
}

public interface IProfileSessionCoordinator { void CancelAll(); }

public sealed class ManagedCommandRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<IManagedProcess>> _live = new(StringComparer.Ordinal);
    private readonly IProfileSessionCoordinator? _coordinator;

    public ManagedCommandRegistry(IProfileSessionCoordinator? coordinator = null) => _coordinator = coordinator;

    public void Register(string toolId, IManagedProcess process)
    {
        ArgumentNullException.ThrowIfNull(toolId); ArgumentNullException.ThrowIfNull(process);
        lock (_gate) { if (!_live.TryGetValue(toolId, out var list)) _live[toolId] = list = new(); list.Add(process); }
        process.Exited += (_, _) => Remove(toolId, process);
    }

    public IReadOnlyList<IManagedProcess> Live(string toolId)
    {
        lock (_gate) return _live.TryGetValue(toolId, out var list) ? list.ToArray() : Array.Empty<IManagedProcess>();
    }

    public void RegisterApplication(string toolId, IManagedProcess process) { /* Applications are not managed commands. */ }

    public void StopAll()
    {
        _coordinator?.CancelAll();
        IManagedProcess[] snapshot;
        lock (_gate) snapshot = _live.Values.SelectMany(x => x).ToArray();
        foreach (var process in snapshot) { try { process.KillTree(); } catch { } }
    }

    private void Remove(string toolId, IManagedProcess process)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(toolId, out var list)) return;
            list.Remove(process);
            if (list.Count == 0) _live.Remove(toolId);
        }
    }
}
