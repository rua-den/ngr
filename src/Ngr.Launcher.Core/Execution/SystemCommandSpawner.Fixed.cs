using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Execution;

public sealed class SystemCommandSpawner : ICommandSpawner
{
    private const long MaxLogContentBytes = 10L * 1024 * 1024;
    private const int RetainedLogCount = 10;
    private static readonly object RetentionGate = new();

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

        // The OS owns desktop application lifetime after launch.
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
                process.OutputDataReceived += (_, eventArgs) =>
                    WriteOutput(log, CommandOutputStream.StdOut, eventArgs.Data);
                process.ErrorDataReceived += (_, eventArgs) =>
                    WriteOutput(log, CommandOutputStream.StdErr, eventArgs.Data);
            }

            if (!process.Start())
            {
                throw new InvalidOperationException($"Windows did not start command '{tool.Name}'.");
            }

            started = true;
            if (specification.RedirectOutput)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            var managedProcess = new SystemManagedProcess(process, log);
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
        var log = new SynchronizedCommandLog(new CappedCommandLog(stream, MaxLogContentBytes));

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

    private static void WriteOutput(
        SynchronizedCommandLog? log,
        CommandOutputStream stream,
        string? text)
    {
        if (log is null || text is null)
        {
            return;
        }

        log.Write(CommandLogFormatter.Format(DateTimeOffset.UtcNow, stream, text));
    }

    private static string SafePathSegment(string toolId)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(toolId
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized) || sanitized is "." or "..")
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(toolId)));
            return $"tool-{hash[..12]}";
        }

        return sanitized;
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

    private sealed class SynchronizedCommandLog(CappedCommandLog inner) : IDisposable
    {
        private readonly object _gate = new();
        private CappedCommandLog? _inner = inner ?? throw new ArgumentNullException(nameof(inner));

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
            }
        }
    }

    private sealed class SystemManagedProcess(
        Process process,
        SynchronizedCommandLog? log) : IManagedProcess
    {
        private readonly Process _process = process ?? throw new ArgumentNullException(nameof(process));
        private readonly SynchronizedCommandLog? _log = log;
        private int _observationStarted;
        private int _completionSignalled;

        public event EventHandler? Exited;

        public void BeginObserving()
        {
            if (Interlocked.Exchange(ref _observationStarted, 1) != 0)
            {
                throw new InvalidOperationException("Process observation already started.");
            }

            _ = ObserveAsync();
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
            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
                // Completes asynchronous stdout/stderr event processing as well.
                _process.WaitForExit();
            }
            finally
            {
                _log?.Dispose();
                if (Interlocked.Exchange(ref _completionSignalled, 1) == 0)
                {
                    Exited?.Invoke(this, EventArgs.Empty);
                }

                _process.Dispose();
            }
        }
    }
}
