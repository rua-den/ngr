using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Execution;

public interface ICommandSpawner
{
    CommandInstance Start(ToolDefinition tool);
}

public sealed record CommandInstance(string ToolId, Guid InstanceId);

public interface IDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public enum StepRunStatus
{
    Started,
    Failed,
    Cancelled
}

public sealed record StepRunResult(string ToolId, StepRunStatus Status, string? Error = null);

public sealed record ProfileRunResult(Guid SessionId, IReadOnlyList<StepRunResult> Steps);

public sealed record ProfileProgress(Guid SessionId, string ToolId, int Index, int Total);

public sealed class ProfileRunner(ICommandSpawner spawner, IDelay delay)
{
    private readonly ICommandSpawner _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
    private readonly IDelay _delay = delay ?? throw new ArgumentNullException(nameof(delay));

    public Task<ProfileRunResult> RunAsync(
        ProfileDefinition profile,
        IEnumerable<ToolDefinition> tools,
        Action<ProfileProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tools);

        var toolSnapshot = tools
            .Select(CloneTool)
            .GroupBy(tool => tool.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var steps = profile.Steps
            .Select(step => new SnapshotStep(step.ToolId, step.DelayBeforeSeconds,
                toolSnapshot.GetValueOrDefault(step.ToolId)))
            .ToArray();
        var sessionId = Guid.NewGuid();

        return RunSnapshotAsync(sessionId, steps, progress, cancellationToken);
    }

    private async Task<ProfileRunResult> RunSnapshotAsync(
        Guid sessionId,
        IReadOnlyList<SnapshotStep> steps,
        Action<ProfileProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<StepRunResult>(steps.Count);

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            ReportProgress(progress, new ProfileProgress(sessionId, step.ToolId, index + 1, steps.Count));

            try
            {
                await _delay.DelayAsync(
                    TimeSpan.FromSeconds(step.DelayBeforeSeconds),
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (step.Tool is null)
                {
                    results.Add(new StepRunResult(step.ToolId, StepRunStatus.Failed, "Tool not found."));
                    continue;
                }

                _spawner.Start(step.Tool);
                results.Add(new StepRunResult(step.ToolId, StepRunStatus.Started));
            }
            catch (OperationCanceledException)
            {
                AddCancelledSteps(results, steps, index);
                break;
            }
            catch (Exception exception)
            {
                results.Add(new StepRunResult(step.ToolId, StepRunStatus.Failed, exception.Message));
            }
        }

        return new ProfileRunResult(sessionId, results);
    }

    private static void ReportProgress(Action<ProfileProgress>? progress, ProfileProgress update)
    {
        if (progress is null)
        {
            return;
        }

        try
        {
            progress(update);
        }
        catch
        {
            // Progress is an observer only. UI/reporting failures must not change execution semantics.
        }
    }

    private static void AddCancelledSteps(
        ICollection<StepRunResult> results,
        IReadOnlyList<SnapshotStep> steps,
        int firstCancelledIndex)
    {
        for (var index = firstCancelledIndex; index < steps.Count; index++)
        {
            results.Add(new StepRunResult(steps[index].ToolId, StepRunStatus.Cancelled));
        }
    }

    private static ToolDefinition CloneTool(ToolDefinition tool) => tool with
    {
        EnvironmentVariables = new Dictionary<string, string>(
            tool.EnvironmentVariables,
            StringComparer.OrdinalIgnoreCase)
    };

    private sealed record SnapshotStep(string ToolId, int DelayBeforeSeconds, ToolDefinition? Tool);
}
