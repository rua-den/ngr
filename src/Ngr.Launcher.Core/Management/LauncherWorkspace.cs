using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.Core.Management;

public sealed class LauncherWorkspace(JsonConfigurationStore store)
{
    private readonly JsonConfigurationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly SemaphoreSlim _mutationGate = new(1, 1);

    public AppConfiguration Configuration { get; private set; } = AppConfiguration.CreateDefault();

    public event EventHandler? Changed;

    public async Task<ConfigurationLoadResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        Configuration = result.Configuration;
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public Task SaveToolAsync(
        string? originalId,
        ToolDefinition tool,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var validationErrors = ToolValidator.Validate(tool);
        if (validationErrors.Count > 0)
        {
            throw new System.IO.InvalidDataException(
                $"Tool is invalid: {string.Join(", ", validationErrors)}.");
        }

        var snapshot = Clone(tool);
        return MutateAsync(configuration =>
        {
            var tools = configuration.Tools.Select(Clone).ToList();
            var profiles = configuration.Profiles.Select(Clone).ToArray();

            if (originalId is null)
            {
                if (tools.Any(existing => string.Equals(existing.Id, snapshot.Id, StringComparison.Ordinal)))
                {
                    throw new System.IO.InvalidDataException($"Tool ID '{snapshot.Id}' already exists.");
                }

                tools.Add(snapshot);
            }
            else
            {
                var index = tools.FindIndex(existing =>
                    string.Equals(existing.Id, originalId, StringComparison.Ordinal));
                if (index < 0)
                {
                    throw new KeyNotFoundException($"Tool '{originalId}' no longer exists.");
                }

                if (!string.Equals(originalId, snapshot.Id, StringComparison.Ordinal)
                    && tools.Any(existing => string.Equals(existing.Id, snapshot.Id, StringComparison.Ordinal)))
                {
                    throw new System.IO.InvalidDataException($"Tool ID '{snapshot.Id}' already exists.");
                }

                tools[index] = snapshot;
                if (!string.Equals(originalId, snapshot.Id, StringComparison.Ordinal))
                {
                    profiles = profiles
                        .Select(profile => profile with
                        {
                            Steps = profile.Steps
                                .Select(step => string.Equals(step.ToolId, originalId, StringComparison.Ordinal)
                                    ? step with { ToolId = snapshot.Id }
                                    : step)
                                .ToArray()
                        })
                        .ToArray();
                }
            }

            return configuration with
            {
                Tools = tools.ToArray(),
                Profiles = profiles
            };
        }, cancellationToken);
    }

    public Task RemoveToolAsync(string toolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        return MutateAsync(configuration =>
        {
            var referencedBy = configuration.Profiles
                .Where(profile => profile.Steps.Any(step =>
                    string.Equals(step.ToolId, toolId, StringComparison.Ordinal)))
                .Select(profile => profile.Name)
                .ToArray();
            if (referencedBy.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Tool '{toolId}' is used by: {string.Join(", ", referencedBy)}.");
            }

            var tools = configuration.Tools
                .Where(tool => !string.Equals(tool.Id, toolId, StringComparison.Ordinal))
                .Select(Clone)
                .ToArray();
            if (tools.Length == configuration.Tools.Count)
            {
                throw new KeyNotFoundException($"Tool '{toolId}' no longer exists.");
            }

            return configuration with { Tools = tools };
        }, cancellationToken);
    }

    public Task SaveProfileAsync(
        string? originalId,
        ProfileDefinition profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var validationErrors = ProfileValidator.Validate(profile, Configuration.Tools);
        if (validationErrors.Count > 0)
        {
            throw new System.IO.InvalidDataException(
                $"Profile is invalid: {string.Join(", ", validationErrors)}.");
        }

        var snapshot = Clone(profile);
        return MutateAsync(configuration =>
        {
            var profiles = configuration.Profiles.Select(Clone).ToList();
            if (originalId is null)
            {
                if (profiles.Any(existing => string.Equals(existing.Id, snapshot.Id, StringComparison.Ordinal)))
                {
                    throw new System.IO.InvalidDataException($"Profile ID '{snapshot.Id}' already exists.");
                }

                profiles.Add(snapshot);
            }
            else
            {
                var index = profiles.FindIndex(existing =>
                    string.Equals(existing.Id, originalId, StringComparison.Ordinal));
                if (index < 0)
                {
                    throw new KeyNotFoundException($"Profile '{originalId}' no longer exists.");
                }

                if (!string.Equals(originalId, snapshot.Id, StringComparison.Ordinal)
                    && profiles.Any(existing => string.Equals(existing.Id, snapshot.Id, StringComparison.Ordinal)))
                {
                    throw new System.IO.InvalidDataException($"Profile ID '{snapshot.Id}' already exists.");
                }

                profiles[index] = snapshot;
            }

            return configuration with { Profiles = profiles.ToArray() };
        }, cancellationToken);
    }

    public Task RemoveProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        return MutateAsync(configuration =>
        {
            var profiles = configuration.Profiles
                .Where(profile => !string.Equals(profile.Id, profileId, StringComparison.Ordinal))
                .Select(Clone)
                .ToArray();
            if (profiles.Length == configuration.Profiles.Count)
            {
                throw new KeyNotFoundException($"Profile '{profileId}' no longer exists.");
            }

            return configuration with { Profiles = profiles };
        }, cancellationToken);
    }

    public Task UpdateSettingsAsync(
        LauncherSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return MutateAsync(
            configuration => configuration with { Settings = settings },
            cancellationToken);
    }

    private async Task MutateAsync(
        Func<AppConfiguration, AppConfiguration> mutation,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidate = mutation(Configuration);
            AppConfigurationValidator.Validate(candidate);
            await _store.SaveAsync(candidate, cancellationToken).ConfigureAwait(false);
            Configuration = candidate;
        }
        finally
        {
            _mutationGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static ToolDefinition Clone(ToolDefinition tool) => tool with
    {
        EnvironmentVariables = new Dictionary<string, string>(
            tool.EnvironmentVariables,
            StringComparer.OrdinalIgnoreCase)
    };

    private static ProfileDefinition Clone(ProfileDefinition profile) => profile with
    {
        Steps = profile.Steps.Select(step => step with { }).ToArray()
    };
}
