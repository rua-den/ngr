using System.Diagnostics;
using System.Text;
using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class SystemCommandSpawnerIntegrationBehaviorTests
{
    [Fact]
    public async Task Hidden_command_captures_timestamped_stdout_and_stderr()
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-process-tests-").FullName;
        try
        {
            var registry = new ManagedCommandRegistry();
            var spawner = new SystemCommandSpawner(registry, root);
            var tool = HiddenCommand(
                "capture",
                "echo ngr-stdout-token & echo ngr-stderr-token 1>&2");

            spawner.Start(tool);

            var log = await WaitForLogAsync(Path.Combine(root, tool.Id),
                text => text.Contains("STDOUT ngr-stdout-token", StringComparison.Ordinal)
                    && text.Contains("STDERR ngr-stderr-token", StringComparison.Ordinal));

            Assert.Contains("[", log, StringComparison.Ordinal);
            Assert.Contains("+00:00] STDOUT", log, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Starting_hidden_command_retains_only_latest_ten_logs_for_its_tool()
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-retention-tests-").FullName;
        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "retained")).FullName;
            for (var index = 0; index < 10; index++)
            {
                var oldLog = Path.Combine(toolDirectory, $"old-{index:D2}.log");
                await File.WriteAllTextAsync(oldLog, "old");
                File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-(index + 1)));
            }

            var registry = new ManagedCommandRegistry();
            var spawner = new SystemCommandSpawner(registry, root);
            spawner.Start(HiddenCommand("retained", "echo newest-token"));

            await WaitUntilAsync(
                () => Directory.EnumerateFiles(toolDirectory, "*.log").Any(path =>
                    TryReadSharedText(path, out var content)
                    && content.Contains("newest-token", StringComparison.Ordinal)),
                TimeSpan.FromSeconds(10));

            Assert.Equal(10, Directory.EnumerateFiles(toolDirectory, "*.log").Count());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Stop_all_kills_the_entire_hidden_command_process_tree()
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-tree-tests-").FullName;
        var childIdFile = Path.Combine(root, "child.pid");
        int? childProcessId = null;

        try
        {
            var escapedIdFile = childIdFile.Replace("'", "''", StringComparison.Ordinal);
            var command = string.Join(' ',
                "$child = Start-Process -FilePath ping.exe",
                "-ArgumentList '127.0.0.1','-n','30' -PassThru -WindowStyle Hidden;",
                $"$child.Id | Set-Content -LiteralPath '{escapedIdFile}';",
                "Wait-Process -Id $child.Id");
            var registry = new ManagedCommandRegistry();
            var spawner = new SystemCommandSpawner(registry, root);
            var tool = HiddenPowerShell("tree", command);

            spawner.Start(tool);
            await WaitUntilAsync(() => File.Exists(childIdFile), TimeSpan.FromSeconds(10));
            childProcessId = int.Parse((await File.ReadAllTextAsync(childIdFile)).Trim(),
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(IsRunning(childProcessId.Value));

            registry.StopAll();

            await WaitUntilAsync(() => !IsRunning(childProcessId.Value), TimeSpan.FromSeconds(10));
            Assert.Empty(registry.Live(tool.Id));
        }
        finally
        {
            if (childProcessId is { } processId && IsRunning(processId))
            {
                Process.GetProcessById(processId).Kill(entireProcessTree: true);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static ToolDefinition HiddenCommand(string id, string commandText) => new()
    {
        Id = id,
        Name = id,
        Kind = ToolKind.Command,
        CommandText = commandText,
        Shell = ShellKind.CommandPrompt,
        WindowMode = CommandWindowMode.Hidden
    };

    private static ToolDefinition HiddenPowerShell(string id, string commandText) => new()
    {
        Id = id,
        Name = id,
        Kind = ToolKind.Command,
        CommandText = commandText,
        Shell = ShellKind.WindowsPowerShell,
        WindowMode = CommandWindowMode.Hidden
    };

    private static async Task<string> WaitForLogAsync(
        string directory,
        Func<string, bool> predicate)
    {
        string? matchingContent = null;
        await WaitUntilAsync(() =>
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.log"))
            {
                if (TryReadSharedText(path, out var content) && predicate(content))
                {
                    matchingContent = content;
                    return true;
                }
            }

            return false;
        }, TimeSpan.FromSeconds(10));

        return matchingContent!;
    }

    private static bool TryReadSharedText(string path, out string content)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            content = reader.ReadToEnd();
            return true;
        }
        catch (IOException)
        {
            content = string.Empty;
            return false;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Condition was not met within {timeout}.");
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            return !Process.GetProcessById(processId).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
