using System.Collections;
using System.Reflection;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Execution;

public interface ICommandSpawner
{
    CommandInstance Start(ToolDefinition tool);
}

public sealed record CommandInstance(string ToolId, string InstanceId);
public interface IDelay { Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken); }
public enum StepRunStatus { Started, Failed, Cancelled }
public sealed record StepRunResult(string StepId, StepRunStatus Status, string? Error = null);
public sealed record ProfileRunResult(Guid SessionId, IReadOnlyList<StepRunResult> Steps);
public sealed record ProfileProgress(Guid SessionId, string StepId, int Index, int Total);

public sealed class ProfileRunner
{
    private readonly ICommandSpawner _spawner;
    private readonly IDelay _delay;
    private readonly IProgress<ProfileProgress>? _progress;

    public ProfileRunner(ICommandSpawner spawner, IDelay delay, IProgress<ProfileProgress>? progress = null)
        => (_spawner, _delay, _progress) = (spawner, delay, progress);

    public Task<ProfileRunResult> RunAsync(object profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var session = Guid.NewGuid();
        var steps = SnapshotSteps(profile);
        return RunSnapshotAsync(session, steps, cancellationToken);
    }

    private async Task<ProfileRunResult> RunSnapshotAsync(Guid session, IReadOnlyList<SnapshotStep> steps, CancellationToken token)
    {
        var results = new List<StepRunResult>(steps.Count);
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            _progress?.Report(new ProfileProgress(session, step.Id, i, steps.Count));
            try
            {
                await _delay.DelayAsync(TimeSpan.FromSeconds(step.DelayBeforeSeconds), token).ConfigureAwait(false);
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                if (step.Tool is null) { results.Add(new(step.Id, StepRunStatus.Failed, "Tool not found.")); continue; }
                _spawner.Start(step.Tool);
                results.Add(new(step.Id, StepRunStatus.Started));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                results.Add(new(step.Id, StepRunStatus.Cancelled));
                for (i++; i < steps.Count; i++) results.Add(new(steps[i].Id, StepRunStatus.Cancelled));
                break;
            }
            catch (Exception ex) { results.Add(new(step.Id, StepRunStatus.Failed, ex.Message)); }
        }
        return new ProfileRunResult(session, results);
    }

    private static IReadOnlyList<SnapshotStep> SnapshotSteps(object profile)
    {
        var value = Property(profile, "Steps") as IEnumerable ?? Array.Empty<object>();
        var result = new List<SnapshotStep>();
        foreach (var step in value)
        {
            var id = Property(step!, "Id", "StepId", "Name")?.ToString() ?? string.Empty;
            var delay = Convert.ToDouble(Property(step!, "DelayBeforeSeconds") ?? 0, System.Globalization.CultureInfo.InvariantCulture);
            var tool = Property(step!, "Tool") as ToolDefinition;
            result.Add(new SnapshotStep(id, delay, tool));
        }
        return result;
    }

    private static object? Property(object target, params string[] names)
    {
        var type = target.GetType();
        foreach (var name in names)
        {
            var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p is not null) return p.GetValue(target);
        }
        return null;
    }
    private sealed record SnapshotStep(string Id, double DelayBeforeSeconds, ToolDefinition? Tool);
}
