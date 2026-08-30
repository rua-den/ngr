using System.Collections.ObjectModel;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Execution;

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
        var (fileName, arguments) = request.Shell switch
        {
            ShellKind.CommandPrompt => (
                "cmd.exe",
                $"/d /s /{(terminal ? "k" : "c")} {request.CommandText}"),
            ShellKind.WindowsPowerShell => (
                "powershell.exe",
                terminal
                    ? $"-NoLogo -NoProfile -NoExit -Command {request.CommandText}"
                    : $"-NoLogo -NoProfile -NonInteractive -Command {request.CommandText}"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unsupported command shell.")
        };

        return new ProcessLaunchSpec(
            fileName,
            arguments,
            ResolveWorkingDirectory(request.WorkingDirectory),
            MergeEnvironment(request.EnvironmentVariables),
            UseShellExecute: false,
            RedirectOutput: !terminal,
            CreateNoWindow: !terminal);
    }

    public static ProcessLaunchSpec BuildApplication(ApplicationLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ProcessLaunchSpec(
            request.Target,
            request.Arguments ?? string.Empty,
            ResolveWorkingDirectory(request.WorkingDirectory),
            MergeEnvironment(overrides: null),
            UseShellExecute: true,
            RedirectOutput: false,
            CreateNoWindow: false);
    }

    private static string ResolveWorkingDirectory(string? workingDirectory) =>
        string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : workingDirectory;

    private static IReadOnlyDictionary<string, string> MergeEnvironment(
        IReadOnlyDictionary<string, string>? overrides)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is not null)
            {
                environment[key] = entry.Value.ToString()!;
            }
        }

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                environment[key] = value;
            }
        }

        return new ReadOnlyDictionary<string, string>(environment);
    }
}
