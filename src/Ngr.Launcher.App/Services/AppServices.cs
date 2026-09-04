using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Ngr.Launcher.Core.Execution;

namespace Ngr.Launcher.App.Services;

public interface IConfirmationService
{
    bool Confirm(string title, string message);
}

public sealed class WpfConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
}

public sealed record ApplicationTargetSelection(string Target, string DisplayName);

public interface IPathPickerService
{
    ApplicationTargetSelection? PickApplicationTarget(string? currentPath = null);
    string? PickFolder(string? currentPath = null);
}

public sealed class WpfPathPickerService : IPathPickerService
{
    private readonly IInstalledApplicationCatalog _applicationCatalog;

    public WpfPathPickerService() : this(new WindowsInstalledApplicationCatalog())
    {
    }

    public WpfPathPickerService(IInstalledApplicationCatalog applicationCatalog)
    {
        _applicationCatalog = applicationCatalog ?? throw new ArgumentNullException(nameof(applicationCatalog));
    }

    public ApplicationTargetSelection? PickApplicationTarget(string? currentPath = null)
    {
        // Re-discover on every open so newly installed/uninstalled apps are reflected
        // without forcing the user to restart NGR Launcher.
        var applications = _applicationCatalog.Discover();
        var picker = new InstalledApplicationPickerWindow(applications, currentPath);
        if (Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            picker.Owner = owner;
        }

        var result = picker.ShowDialog();
        if (picker.BrowseFileRequested)
        {
            var file = PickApplicationFile(currentPath);
            return file is null
                ? null
                : new ApplicationTargetSelection(file, ResolveFileDisplayName(file));
        }

        if (result != true || string.IsNullOrWhiteSpace(picker.SelectedTarget))
        {
            return null;
        }

        var target = picker.SelectedTarget;
        var entry = applications.FirstOrDefault(candidate =>
            string.Equals(candidate.Target, target, StringComparison.OrdinalIgnoreCase));
        return new ApplicationTargetSelection(
            target,
            entry?.Name ?? ResolveFileDisplayName(target));
    }

    public string? PickFolder(string? currentPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a working directory",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
        {
            dialog.InitialDirectory = currentPath;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string? PickApplicationFile(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an application or file",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Applications and shortcuts|*.exe;*.com;*.bat;*.cmd;*.lnk;*.url|All files|*.*"
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (File.Exists(currentPath))
            {
                dialog.FileName = currentPath;
            }
            else
            {
                var directory = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                }
            }
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string ResolveFileDisplayName(string path)
    {
        if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var version = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(version.FileDescription))
                {
                    return version.FileDescription.Trim();
                }

                if (!string.IsNullOrWhiteSpace(version.ProductName))
                {
                    return version.ProductName.Trim();
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return Path.GetFileNameWithoutExtension(path);
    }
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
