namespace Ngr.Launcher.Core.Execution;

public sealed class SystemDelay : IDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
