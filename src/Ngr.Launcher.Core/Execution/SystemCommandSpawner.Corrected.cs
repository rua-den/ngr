using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Execution;

public sealed class SystemCommandSpawner : ICommandSpawner
{
    private const long MaxLogContentBytes = 10L * 1024 * 1024;
    private const int RetainedLogCount = 10;
    private const int MaxSafeToolDirectoryNameLength = 80;
    private static readonly object RetentionGate = new();
    private static readonly HashSet<string> ReservedWindowsDeviceNames = new(
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly ManagedCommandRegistry _registry;
    private readonly string _logRootDirectory;

    public SystemCommandSpawner(ManagedCommandRegistry registry, string logRootDirectory)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentException.ThrowIfNullOrWhiteSpace(logRootDirectory);
        _logRootDirectory = Path.GetFullPath(logRootDirectory);
    }

    public CommandInstance Start(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.Id);

        return tool.Kind switch
        {
            ToolKind.Application => StartApplication(tool),
            ToolKind.Command => StartCommand(tool),
            _ => throw new ArgumentOutOfRangeException(nameof(tool), "Unsupported tool kind.")
        };
    }

    private static CommandInstance StartApplication(ToolDefinition tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.Target);

        var specification = ShellCommandBuilder.BuildApplication(new ApplicationLaunchRequest(
            tool.Target,
            tool.Arguments,
            tool.WorkingDirectory));
        using var process = CreateProcess(specification);
        if (!process.Start())
        {
            throw new InvalidOperationException($"Windows did not start application '{tool.Name}'.");
        }

        return new CommandInstance(tool.Id, Guid.NewGuid());
    }

    private CommandInstance StartCommand(ToolDefinition tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.CommandText);
        var shell = tool.Shell
            ?? throw new ArgumentException("A command tool must select a shell.", nameof(tool));
        var windowMode = tool.WindowMode ?? CommandWindowMode.Hidden;
        var specification = ShellCommandBuilder.Build(new ShellCommandRequest(
            shell,
            windowMode,
            tool.CommandText,
            tool.WorkingDirectory,
            tool.EnvironmentVariables));

        var process = CreateProcess(specification);
        SynchronizedCommandLog? log = null;
        string? logPath = null;
        var started = false;

        try
        {
            if (specification.RedirectOutput)
            {
                (log, logPath) = CreateLog(tool.Id);
            }

            if (!process.Start())
            {
                throw new InvalidOperationException($"Windows did not start command '{tool.Name}'.");
            }

            started = true;
            var managedProcess = new SystemManagedProcess(
                process,
                log,
                specification.RedirectOutput);
            _registry.Register(tool.Id, managedProcess);
            managedProcess.BeginObserving();

            return new CommandInstance(tool.Id, Guid.NewGuid());
        }
        catch
        {
            if (started)
            {
                TryKillTree(process);
            }

            log?.Dispose();
            process.Dispose();
            TryDeleteFailedLog(logPath);
            throw;
        }
    }

    private (SynchronizedCommandLog Log, string Path) CreateLog(string toolId)
    {
        var toolDirectory = Path.Combine(_logRootDirectory, SafePathSegment(toolId));
        Directory.CreateDirectory(toolDirectory);
        var path = Path.Combine(
            toolDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss.fffffff}-{Guid.NewGuid():N}.log");
        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        var log = new SynchronizedCommandLog(
            new CappedCommandLog(stream, MaxLogContentBytes),
            stream);

        try
        {
            lock (RetentionGate)
            {
                CommandLogRetention.RetainLatest(toolDirectory, RetainedLogCount);
            }

            return (log, path);
        }
        catch
        {
            log.Dispose();
            TryDeleteFailedLog(path);
            throw;
        }
    }

    private static Process CreateProcess(ProcessLaunchSpec specification)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = specification.FileName,
            Arguments = specification.Arguments,
            WorkingDirectory = specification.WorkingDirectory,
            UseShellExecute = specification.UseShellExecute,
            RedirectStandardOutput = specification.RedirectOutput,
            RedirectStandardError = specification.RedirectOutput,
            CreateNoWindow = specification.CreateNoWindow
        };

        if (!specification.UseShellExecute)
        {
            startInfo.Environment.Clear();
            foreach (var (key, value) in specification.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        return new Process { StartInfo = startInfo };
    }

    private static string SafePathSegment(string toolId)
    {
        if (IsSafePathSegment(toolId))
        {
            return toolId;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(toolId)));
        return $"tool-{hash[..24]}";
    }

    private static bool IsSafePathSegment(string toolId)
    {
        if (toolId.Length == 0 || toolId.Length > MaxSafeToolDirectoryNameLength)
        {
            return false;
        }

        if (toolId is "." or ".." || char.IsWhiteSpace(toolId[^1]) || toolId[^1] == '.')
        {
            return false;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        if (toolId.Any(character => char.IsControl(character) || invalidCharacters.Contains(character)))
        {
            return false;
        }

        var deviceName = toolId.Split('.', 2)[0];
        return !ReservedWindowsDeviceNames.Contains(deviceName);
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static void TryDeleteFailedLog(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class SynchronizedCommandLog(
        CappedCommandLog inner,
        Stream ownedStream) : IDisposable
    {
        private readonly object _gate = new();
        private CappedCommandLog? _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        private Stream? _ownedStream = ownedStream ?? throw new ArgumentNullException(nameof(ownedStream));

        public void Write(string line)
        {
            lock (_gate)
            {
                _inner?.Write(line);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _inner?.Dispose();
                _inner = null;
                _ownedStream?.Dispose();
                _ownedStream = null;
            }
        }
    }

    private sealed class SystemManagedProcess(
        Process process,
        SynchronizedCommandLog? log,
        bool captureOutput) : IManagedProcess
    {
        private readonly Process _process = process ?? throw new ArgumentNullException(nameof(process));
        private readonly SynchronizedCommandLog? _log = log;
        private readonly bool _captureOutput = captureOutput;
        private int _observationStarted;
        private int _completionSignalled;

        public event EventHandler? Exited;

        public void BeginObserving()
        {
            if (Interlocked.Exchange(ref _observationStarted, 1) == 0)
            {
                _ = ObserveAsync();
            }
        }

        public void KillTree()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process completed between the state check and kill request.
            }
        }

        private async Task ObserveAsync()
        {
            var standardOutput = _captureOutput
                ? PumpAsync(_process.StandardOutput, CommandOutputStream.StdOut)
                : Task.CompletedTask;
            var standardError = _captureOutput
                ? PumpAsync(_process.StandardError, CommandOutputStream.StdErr)
                : Task.CompletedTask;

            try
            {
                await Task.WhenAll(
                    _process.WaitForExitAsync(),
                    standardOutput,
                    standardError).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    _log?.Dispose();
                }
                finally
                {
                    if (Interlocked.Exchange(ref _completionSignalled, 1) == 0)
                    {
                        Exited?.Invoke(this, EventArgs.Empty);
                    }

                    _process.Dispose();
                }
            }
        }

        private async Task PumpAsync(StreamReader reader, CommandOutputStream stream)
        {
            try
            {
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    _log?.Write(CommandLogFormatter.Format(DateTimeOffset.UtcNow, stream, line));
                }
            }
            catch (IOException)
            {
                // A force-killed process can close redirected pipes during a read.
            }
            catch (ObjectDisposedException)
            {
                // Cleanup won a race with a pending pipe read.
            }
        }
    }
}
