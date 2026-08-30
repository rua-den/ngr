using Ngr.Launcher.Core.Configuration;
using Xunit;

namespace Ngr.Launcher.Core.Tests;

public sealed class ConfigurationStoreBehaviorTests
{
    [Fact]
    public async Task Round_trip_persists_configuration()
    {
        await using var directory = new StoreTemporaryDirectory();
        var store = new JsonConfigurationStore(directory.Path);
        var defaults = AppConfiguration.CreateDefault();
        var expected = defaults with
        {
            Settings = defaults.Settings with { LogRetentionRuns = 25 }
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual.Configuration);
        Assert.Empty(actual.Warnings);
    }

    [Fact]
    public async Task Save_uses_atomic_replace_and_keeps_one_backup()
    {
        await using var directory = new StoreTemporaryDirectory();
        var store = new JsonConfigurationStore(directory.Path);
        var defaults = AppConfiguration.CreateDefault();
        await store.SaveAsync(defaults);
        await store.SaveAsync(defaults with
        {
            Settings = defaults.Settings with { LogRetentionRuns = 20 }
        });

        Assert.True(File.Exists(Path.Combine(directory.Path, "config.json")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "config.json.bak")));
        Assert.False(File.Exists(Path.Combine(directory.Path, "config.json.tmp")));
    }

    [Fact]
    public async Task Corrupt_main_is_recovered_from_backup_with_warning()
    {
        await using var directory = new StoreTemporaryDirectory();
        var store = new JsonConfigurationStore(directory.Path);
        var expected = AppConfiguration.CreateDefault();
        await store.SaveAsync(expected);
        await store.SaveAsync(expected with { Settings = expected.Settings with { LogRetentionRuns = 11 } });
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "config.json"), "{broken");

        var result = await store.LoadAsync();

        Assert.Equal(expected, result.Configuration);
        Assert.Contains(result.Warnings, warning => warning.Contains("backup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Corrupt_main_and_backup_are_renamed_and_empty_configuration_returned()
    {
        await using var directory = new StoreTemporaryDirectory();
        var store = new JsonConfigurationStore(directory.Path);
        await store.SaveAsync(AppConfiguration.CreateDefault());
        await store.SaveAsync(AppConfiguration.CreateDefault());
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "config.json"), "{broken");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "config.json.bak"), "also broken");

        var result = await store.LoadAsync();

        Assert.Equal(AppConfiguration.CreateDefault(), result.Configuration);
        Assert.NotEmpty(result.Warnings);
        Assert.NotEmpty(Directory.GetFiles(directory.Path, "config.json.corrupt-*"));
    }

    private sealed class StoreTemporaryDirectory : IAsyncDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ngr-launcher-tests",
            Guid.NewGuid().ToString("N"));

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
