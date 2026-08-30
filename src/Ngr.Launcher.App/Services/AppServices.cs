using System.Collections.Concurrent;
using System.Windows;
using Ngr.Launcher.Core.Execution;

namespace Ngr.Launcher.App.Services;

public interface IConfirmationService
{
    bool Confirm(string title, string message);
}

public sealed class WpfConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}

public interface IUiDispatcher
{
    void Invoke(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}

public sealed class InlineUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}

public sealed class ProfileCancellationRegistry : IProfileSessionCoordinator
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sessions = new();

    public (Guid Id, CancellationToken Token) CreateSession()
    {
        var id = Guid.NewGuid();
        var source = new CancellationTokenSource();
        if (!_sessions.TryAdd(id, source))
        {
            source.Dispose();
            throw new InvalidOperationException("Unable to allocate a profile session.");
        }

        return (id, source.Token);
    }

    public bool Cancel(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var source))
        {
            return false;
        }

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Complete(Guid id)
    {
        if (_sessions.TryRemove(id, out var source))
        {
            source.Dispose();
        }
    }

    public void CancelAll()
    {
        foreach (var source in _sessions.Values)
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
