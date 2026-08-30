using System.Text.RegularExpressions;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Configuration;

public enum ToolValidationError
{
    IdRequired,
    NameRequired,
    TargetRequired,
    CommandTextRequired,
    ShellRequired,
    WindowModeRequired,
    IncompatibleField,
    EnvironmentKeyInvalid,
    WorkingDirectoryInvalid
}

public static partial class ToolValidator
{
    public static IReadOnlyList<ToolValidationError> Validate(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var errors = new HashSet<ToolValidationError>();

        if (string.IsNullOrWhiteSpace(tool.Id))
        {
            errors.Add(ToolValidationError.IdRequired);
        }

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            errors.Add(ToolValidationError.NameRequired);
        }

        if (!string.IsNullOrWhiteSpace(tool.WorkingDirectory) && !IsValidPath(tool.WorkingDirectory))
        {
            errors.Add(ToolValidationError.WorkingDirectoryInvalid);
        }

        if (tool.EnvironmentVariables.Keys.Any(key => !EnvironmentKeyPattern().IsMatch(key)))
        {
            errors.Add(ToolValidationError.EnvironmentKeyInvalid);
        }

        if (tool.Kind == ToolKind.Application)
        {
            if (string.IsNullOrWhiteSpace(tool.Target))
            {
                errors.Add(ToolValidationError.TargetRequired);
            }

            if (!string.IsNullOrWhiteSpace(tool.CommandText)
                || tool.Shell is not null
                || tool.WindowMode is not null
                || tool.EnvironmentVariables.Count > 0)
            {
                errors.Add(ToolValidationError.IncompatibleField);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(tool.CommandText))
            {
                errors.Add(ToolValidationError.CommandTextRequired);
            }

            if (tool.Shell is null)
            {
                errors.Add(ToolValidationError.ShellRequired);
            }

            if (tool.WindowMode is null)
            {
                errors.Add(ToolValidationError.WindowModeRequired);
            }

            if (!string.IsNullOrWhiteSpace(tool.Target) || !string.IsNullOrWhiteSpace(tool.Arguments))
            {
                errors.Add(ToolValidationError.IncompatibleField);
            }
        }

        return errors.OrderBy(error => error).ToArray();
    }

    private static bool IsValidPath(string path)
    {
        try
        {
            _ = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentKeyPattern();
}

public enum ProfileValidationError
{
    IdRequired,
    NameRequired,
    StepsRequired,
    ToolIdRequired,
    ToolNotFound,
    DelayOutOfRange
}

public static class ProfileValidator
{
    public static IReadOnlyList<ProfileValidationError> Validate(
        ProfileDefinition profile,
        IEnumerable<ToolDefinition> availableTools)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(availableTools);

        var errors = new HashSet<ProfileValidationError>();
        var knownToolIds = availableTools.Select(tool => tool.Id).ToHashSet(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            errors.Add(ProfileValidationError.IdRequired);
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add(ProfileValidationError.NameRequired);
        }

        if (profile.Steps.Count == 0)
        {
            errors.Add(ProfileValidationError.StepsRequired);
        }

        foreach (var step in profile.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.ToolId))
            {
                errors.Add(ProfileValidationError.ToolIdRequired);
            }
            else if (!knownToolIds.Contains(step.ToolId))
            {
                errors.Add(ProfileValidationError.ToolNotFound);
            }

            if (step.DelayBeforeSeconds is < 0 or > 300)
            {
                errors.Add(ProfileValidationError.DelayOutOfRange);
            }
        }

        return errors.OrderBy(error => error).ToArray();
    }
}
