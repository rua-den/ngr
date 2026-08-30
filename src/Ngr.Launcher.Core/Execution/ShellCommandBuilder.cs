using System.Collections.ObjectModel;

namespace Ngr.Launcher.Core.Execution;

public enum ShellKind { Cmd, PowerShell }
public enum CommandWindowMode { Hidden, Terminal }

public sealed record ShellCommandRequest(
    ShellKind Shell,
    CommandWindowMode WindowMode,
    string CommandText,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);

public sealed record ApplicationLaunchRequest(
    string Target,
    string? Arguments = null,
    string? WorkingDirectory = null);

public sealed record ProcessLaunchSpec(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    bool UseShellExecute,
    bool RedirectOutput,
    bool CreateNoWindow);

public static class ShellCommandBuilder
{
    public static ProcessLaunchSpec Build(ShellCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var terminal = request.WindowMode == CommandWindowMode.Terminal;
        var args = request.Shell switch
        {
            ShellKind.Cmd => $"/d /s /{(terminal ? "k" : "c")} {request.CommandText}",
            ShellKind.PowerShell => $"-NoLogo -NoProfile -NonInteractive {(terminal ? "-NoExit" : "-Command")} {request.CommandText}",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Shell))
        };
        return new ProcessLaunchSpec(
            request.Shell == ShellKind.Cmd ? "cmd.exe" : "powershell.exe",
            args,
            request.WorkingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            MergeEnvironment(request.EnvironmentVariables),
            false, true, !terminal);
    }

    public static ProcessLaunchSpec Build(ApplicationLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ProcessLaunchSpec(request.Target, request.Arguments ?? string.Empty,
            request.WorkingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            MergeEnvironment(null), true, false, false);
    }

    private static IReadOnlyDictionary<string, string> MergeEnvironment(IReadOnlyDictionary<string, string>? overrides)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            if (entry.Key is string key && entry.Value is not null) result[key] = entry.Value.ToString()!;
        if (overrides is not null)
            foreach (var pair in overrides) result[pair.Key] = pair.Value;
        return new ReadOnlyDictionary<string, string>(result);
    }
}
