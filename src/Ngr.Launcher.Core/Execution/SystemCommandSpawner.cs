using System.Diagnostics;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Execution;

public sealed class SystemCommandSpawner : ICommandSpawner
{
    private const long MaxLogBytes = 10L * 1024 * 1024;
    private const int RetainedLogCount = 10;
    private readonly ManagedCommandRegistry _registry;
    private readonly string _logRootDirectory;

    public SystemCommandSpawner(ManagedCommandRegistry registry, string logRootDirectory)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentException.ThrowIfNullOrWhiteSpace(logRootDirectory);
        _logRootDirectory = logRootDirectory;
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
        if (string.IsNullOrWhiteSpace(tool.Target))
        {
            throw new ArgumentException("Application target is required.", nameof(tool));
        }

        var spec = ShellCommandBuilder.BuildApplication(new ApplicationLaunchRequest(
            tool.Target, tool.Arguments, tool.WorkingDirectory));
        using var process = CreateProcess(spec);
        process.Start();
        // Applications are intentionally not registered or lifecycle-managed.
        return new CommandInstance(tool.Id, Guid.NewGuid());
    }

    private CommandInstance StartCommand(ToolDefinition tool)
    {
        if (tool.Shell is not { } shell || string.IsNullOrWhiteSpace(tool.CommandText))
        {
            throw new ArgumentException("Command shell and command text are required.", nameof(tool));
        }

        var windowMode = tool.WindowMode ?? CommandWindowMode.Hidden;
        var spec = ShellCommandBuilder.Build(new ShellCommandRequest(
            shell switch
            {
                ShellKind.CommandPrompt => Core.Execution.ShellKind.CommandPrompt,
                ShellKind.WindowsPowerShell => Core.Execution.ShellKind.WindowsPowerShell,
                _ => throw new ArgumentOutOfRangeException(nameof(tool))
            },
            windowMode switch
            {
                Models.CommandWindowMode.Hidden => Core.Execution.CommandWindowMode.Hidden,
                Models.CommandWindowMode.Terminal => Core.Execution.CommandWindowMode.Terminal,
                _ => throw new ArgumentOutOfRangeException(nameof(tool))
            },
            tool.CommandText,
            tool.WorkingDirectory,
            tool.EnvironmentVariables));

        var process = CreateProcess(spec);
        var managed = new SystemManagedProcess(process);
        CappedCommandLog? log = null;
        try
        {
            if (spec.RedirectOutput)
            {
                var directory = Directory.CreateDirectory(Path.Combine(_logRootDirectory, tool.Id));
                var path = Path.Combine(directory.FullName,
                    $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.log");
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                log = new CappedCommandLog(stream, MaxLogBytes);
                _ = CommandLogRetention.RetainLatestAsync(directory.FullName, RetainedLogCount);
                process.OutputDataReceived += (_, args) => WriteOutput(log, CommandOutputStream.StdOut, args);
                process.ErrorDataReceived += (_, args) => WriteOutput(log, CommandOutputStream.StdErr, args);
            }

            // Subscribe the registry before starting the OS process so an immediate exit is observed.
            _registry.Register(tool.Id, managed);
            process.EnableRaisingEvents = true;
            process.Start();
            if (spec.RedirectOutput)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            return new CommandInstance(tool.Id, Guid.NewGuid());
        }
        catch
        {
            log?.Dispose();
            process.Dispose();
            throw;
        }
    }

    private static Process CreateProcess(ProcessLaunchSpec spec)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = spec.UseShellExecute,
            RedirectStandardOutput = spec.RedirectOutput,
            RedirectStandardError = spec.RedirectOutput,
            CreateNoWindow = spec.CreateNoWindow
        };
        foreach (var pair in spec.Environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return new Process { StartInfo = startInfo };
    }

    private static void WriteOutput(CappedCommandLog? log, CommandOutputStream stream,
        DataReceivedEventArgs args)
    {
        if (log is null || args.Data is null)
        {
            return;
        }

        try
        {
            log.Write(CommandLogFormatter.Format(DateTimeOffset.UtcNow, stream, args.Data));
        }
        catch (ObjectDisposedException)
        {
            // Process output can race with final cleanup.
        }
    }

    private sealed class SystemManagedProcess(Process process) : IManagedProcess, IDisposable
    {
        public event EventHandler? Exited;

        public void KillTree()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        public void Dispose() => process.Dispose();

        public SystemManagedProcess : this(process)
        {
            process.Exited += (_, args) => Exited?.Invoke(this, args);
        }
    }
}
