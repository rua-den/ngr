using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Management;

public sealed record ToolTemplate(string Key, string Name, ToolDefinition Definition);

public static class ToolTemplates
{
    public static IReadOnlyList<ToolTemplate> All { get; } =
    [
        new(
            "application",
            "Generic application",
            new ToolDefinition
            {
                Id = "application",
                Name = "Application",
                Kind = ToolKind.Application,
                Target = @"C:\Path\To\Application.exe"
            }),
        new(
            "cli",
            "Generic CLI",
            new ToolDefinition
            {
                Id = "command",
                Name = "Command",
                Kind = ToolKind.Command,
                CommandText = "echo hello",
                Shell = ShellKind.CommandPrompt,
                WindowMode = CommandWindowMode.Hidden
            }),
        new(
            "teams",
            "Microsoft Teams",
            new ToolDefinition
            {
                Id = "teams",
                Name = "Microsoft Teams",
                Kind = ToolKind.Application,
                Target = "msteams:"
            }),
        new(
            "avrea",
            "Avrea CLI",
            new ToolDefinition
            {
                Id = "avrea",
                Name = "Avrea",
                Kind = ToolKind.Command,
                CommandText = "avr run list",
                Shell = ShellKind.CommandPrompt,
                WindowMode = CommandWindowMode.Terminal
            }),
        new(
            "ngrok",
            "ngrok",
            new ToolDefinition
            {
                Id = "ngrok",
                Name = "ngrok",
                Kind = ToolKind.Command,
                CommandText = "ngrok http 3000",
                Shell = ShellKind.CommandPrompt,
                WindowMode = CommandWindowMode.Terminal
            }),
        new(
            "npm",
            "npm",
            new ToolDefinition
            {
                Id = "npm",
                Name = "npm dev server",
                Kind = ToolKind.Command,
                CommandText = "npm run dev",
                Shell = ShellKind.CommandPrompt,
                WindowMode = CommandWindowMode.Terminal
            })
    ];

    public static ToolDefinition Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var template = All.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            throw new KeyNotFoundException($"Unknown tool template '{key}'.");
        }

        return Clone(template.Definition);
    }

    private static ToolDefinition Clone(ToolDefinition tool) => tool with
    {
        EnvironmentVariables = new Dictionary<string, string>(
            tool.EnvironmentVariables,
            StringComparer.OrdinalIgnoreCase)
    };
}
