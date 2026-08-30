using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Configuration;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed record LauncherSettings
{
    public bool StartupPromptAnswered { get; init; }

    public bool StartWithWindows { get; init; }

    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public int LogRetentionRuns { get; init; } = 10;

    public long MaxLogBytes { get; init; } = 10 * 1024 * 1024;
}

public sealed record AppConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public LauncherSettings Settings { get; init; } = new();

    public IReadOnlyList<ToolDefinition> Tools { get; init; } = Array.Empty<ToolDefinition>();

    public IReadOnlyList<ProfileDefinition> Profiles { get; init; } = Array.Empty<ProfileDefinition>();

    public static AppConfiguration CreateDefault() => new();

    public bool Equals(AppConfiguration? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && SchemaVersion == other.SchemaVersion
            && Settings == other.Settings
            && Tools is not null
            && other.Tools is not null
            && Profiles is not null
            && other.Profiles is not null
            && Tools.SequenceEqual(other.Tools)
            && Profiles.SequenceEqual(other.Profiles);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        hash.Add(Settings);

        if (Tools is not null)
        {
            foreach (var tool in Tools)
            {
                hash.Add(tool);
            }
        }

        if (Profiles is not null)
        {
            foreach (var profile in Profiles)
            {
                hash.Add(profile);
            }
        }

        return hash.ToHashCode();
    }
}

public static class AppConfigurationValidator
{
    public static void Validate(AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.SchemaVersion != AppConfiguration.CurrentSchemaVersion)
        {
            throw new UnsupportedSchemaVersionException(
                configuration.SchemaVersion,
                AppConfiguration.CurrentSchemaVersion);
        }

        if (configuration.Settings is null)
        {
            throw new System.IO.InvalidDataException("Settings are required.");
        }

        if (!Enum.IsDefined(configuration.Settings.Theme))
        {
            throw new System.IO.InvalidDataException("Theme is invalid.");
        }

        if (configuration.Settings.LogRetentionRuns is < 1 or > 50)
        {
            throw new System.IO.InvalidDataException("Log retention must be between 1 and 50 runs.");
        }

        if (configuration.Settings.MaxLogBytes <= 0)
        {
            throw new System.IO.InvalidDataException("Maximum log size must be positive.");
        }

        if (configuration.Tools is null)
        {
            throw new System.IO.InvalidDataException("Tools are required.");
        }

        if (configuration.Profiles is null)
        {
            throw new System.IO.InvalidDataException("Profiles are required.");
        }

        if (configuration.Tools.Any(tool => tool is null))
        {
            throw new System.IO.InvalidDataException("Tools cannot contain null entries.");
        }

        if (configuration.Profiles.Any(profile => profile is null))
        {
            throw new System.IO.InvalidDataException("Profiles cannot contain null entries.");
        }

        if (HasDuplicateIds(configuration.Tools.Select(tool => tool.Id)))
        {
            throw new System.IO.InvalidDataException("Tool IDs must be unique.");
        }

        if (HasDuplicateIds(configuration.Profiles.Select(profile => profile.Id)))
        {
            throw new System.IO.InvalidDataException("Profile IDs must be unique.");
        }

        foreach (var tool in configuration.Tools)
        {
            if (ToolValidator.Validate(tool).Count > 0)
            {
                throw new System.IO.InvalidDataException($"Tool '{tool.Id}' is invalid.");
            }
        }

        foreach (var profile in configuration.Profiles)
        {
            if (ProfileValidator.Validate(profile, configuration.Tools).Count > 0)
            {
                throw new System.IO.InvalidDataException($"Profile '{profile.Id}' is invalid.");
            }
        }
    }

    private static bool HasDuplicateIds(IEnumerable<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return ids.Any(id => !seen.Add(id));
    }
}

public sealed class UnsupportedSchemaVersionException : Exception
{
    public UnsupportedSchemaVersionException(int actualVersion, int supportedVersion)
        : base($"Configuration schema {actualVersion} is not supported. Expected schema {supportedVersion}.")
    {
        ActualVersion = actualVersion;
        SupportedVersion = supportedVersion;
    }

    public int ActualVersion { get; }

    public int SupportedVersion { get; }
}
