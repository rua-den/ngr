namespace Ngr.Launcher.Core.Models;

public sealed record ProfileStep
{
    public string ToolId { get; init; } = string.Empty;

    public int DelayBeforeSeconds { get; init; }
}

public sealed record ProfileDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<ProfileStep> Steps { get; init; } = Array.Empty<ProfileStep>();

    public bool Equals(ProfileDefinition? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Id == other.Id
            && Name == other.Name
            && Steps.SequenceEqual(other.Steps);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        foreach (var step in Steps)
        {
            hash.Add(step);
        }

        return hash.ToHashCode();
    }
}
