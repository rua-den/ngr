using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class SystemCommandSpawnerLogIsolationTests
{
    [Fact]
    public async Task Distinct_ids_that_sanitize_the_same_are_stored_in_distinct_directories()
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-log-isolation-").FullName;
        try
        {
            var registry = new ManagedCommandRegistry();
            var spawner = new SystemCommandSpawner(registry, root);

            spawner.Start(HiddenCommand("tool:a", "echo colon-token"));
            spawner.Start(HiddenCommand("tool?a", "echo question-token"));
            await WaitUntilAsync(
                () => registry.Live("tool:a").Count == 0 && registry.Live("tool?a").Count == 0,
                TimeSpan.FromSeconds(10));

            var directories = Directory.GetDirectories(root);
            Assert.Equal(2, directories.Length);
            var logsByDirectory = directories
                .Select(directory => string.Join(Environment.NewLine,
                    Directory.GetFiles(directory, "*.log").Select(File.ReadAllText)))
                .ToArray();
            Assert.Single(logsByDirectory, text => text.Contains("colon-token", StringComparison.Ordinal));
            Assert.Single(logsByDirectory, text => text.Contains("question-token", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Reserved_or_long_tool_ids_still_get_a_bounded_safe_log_directory(string toolId)
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-log-path-").FullName;
        try
        {
            var registry = new ManagedCommandRegistry();
            var spawner = new SystemCommandSpawner(registry, root);

            spawner.Start(HiddenCommand(toolId, "echo safe-token"));
            await WaitUntilAsync(() => registry.Live(toolId).Count == 0, TimeSpan.FromSeconds(10));

            var directory = Assert.Single(Directory.GetDirectories(root));
            Assert.True(Path.GetFileName(directory).Length <= 80);
            Assert.Single(Directory.GetFiles(directory, "*.log"));
        }
        finally
        {
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
}
