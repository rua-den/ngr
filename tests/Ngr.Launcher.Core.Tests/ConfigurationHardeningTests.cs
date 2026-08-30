using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests;

public sealed class ConfigurationHardeningTests
{
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"settings\":null,\"tools\":[],\"profiles\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"settings\":{},\"tools\":null,\"profiles\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"settings\":{},\"tools\":[],\"profiles\":null}")]
    public async Task Null_configuration_members_recover_to_defaults(string json)
    {
        await using var directory = new HardeningTemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "config.json"), json);

        var result = await new JsonConfigurationStore(directory.Path).LoadAsync();

        Assert.Equal(AppConfiguration.CreateDefault(), result.Configuration);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Undefined_enum_values_are_rejected()
    {
        var invalidTool = ValidApplication() with { Kind = (ToolKind)999 };
        var invalidTheme = AppConfiguration.CreateDefault() with
        {
            Settings = AppConfiguration.CreateDefault().Settings with { Theme = (ThemePreference)999 }
        };

        Assert.Contains(ToolValidationError.KindInvalid, ToolValidator.Validate(invalidTool));
        Assert.Throws<InvalidDataException>(() => AppConfigurationValidator.Validate(invalidTheme));
    }

    [Fact]
    public void Duplicate_tool_and_profile_ids_are_rejected()
    {
        var tool = ValidApplication();
        var profile = new ProfileDefinition
        {
            Id = "profile-1",
            Name = "Profile",
            Steps = [new ProfileStep { ToolId = tool.Id }]
        };
        var configuration = AppConfiguration.CreateDefault() with
        {
            Tools = [tool, tool with { Name = "Duplicate" }],
            Profiles = [profile, profile with { Name = "Duplicate" }]
        };

        Assert.Throws<InvalidDataException>(() => AppConfigurationValidator.Validate(configuration));
    }

    [Fact]
    public async Task Recovering_from_backup_restores_a_readable_main_file()
    {
        await using var directory = new HardeningTemporaryDirectory();
        var store = new JsonConfigurationStore(directory.Path);
        var expected = AppConfiguration.CreateDefault();
        await store.SaveAsync(expected);
        await store.SaveAsync(expected with { Settings = expected.Settings with { LogRetentionRuns = 11 } });
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "config.json"), "{broken");

        var recovered = await store.LoadAsync();
        File.Delete(Path.Combine(directory.Path, "config.json.bak"));
        var reloaded = await store.LoadAsync();

        Assert.Equal(expected, recovered.Configuration);
        Assert.Equal(expected, reloaded.Configuration);
    }

    private static ToolDefinition ValidApplication() => new()
    {
        Id = "tool-1",
        Name = "Application",
        Kind = ToolKind.Application,
        Target = @"C:\Tools\app.exe"
    };

    private sealed class HardeningTemporaryDirectory : IAsyncDisposable
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
