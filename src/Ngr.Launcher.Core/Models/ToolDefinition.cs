namespace Ngr.Launcher.Core.Models;

public enum ToolKind
{
    Application,
    Command
}

public enum ShellKind
{
    CommandPrompt,
    WindowsPowerShell
}

public enum CommandWindowMode
{
    Hidden,
    Terminal
}

public sealed record ToolDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public ToolKind Kind { get; init; }

    public string? Target { get; init; }

    public string? Arguments { get; init; }

    public string? CommandText { get; init; }

    public ShellKind? Shell { get; init; }

    public CommandWindowMode? WindowMode { get; init; }

    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }
        = new Dictionary<string, string>();

    public string? IconPath { get; init; }

    public bool Equals(ToolDefinition? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Id == other.Id
            && Name == other.Name
            && Kind == other.Kind
            && Target == other.Target
            && Arguments == other.Arguments
            && CommandText == other.CommandText
            && Shell == other.Shell
            && WindowMode == other.WindowMode
            && WorkingDirectory == other.WorkingDirectory
            && IconPath == other.IconPath
            && EnvironmentVariables.Count == other.EnvironmentVariables.Count
            && EnvironmentVariables.All(pair =>
                other.EnvironmentVariables.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        hash.Add(Kind);
        hash.Add(Target);
        hash.Add(Arguments);
        hash.Add(CommandText);
        hash.Add(Shell);
        hash.Add(WindowMode);
        hash.Add(WorkingDirectory);
        hash.Add(IconPath);

        foreach (var pair in EnvironmentVariables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }
}
