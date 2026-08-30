using Ngr.Launcher.Core.Configuration;
using Xunit;

namespace Ngr.Launcher.Core.Tests;

public sealed class AppConfigurationTests
{
    [Fact]
    public void New_configuration_has_schema_one_and_approved_defaults()
    {
        var configuration = AppConfiguration.CreateDefault();

        Assert.Equal(1, configuration.SchemaVersion);
        Assert.False(configuration.Settings.StartupPromptAnswered);
        Assert.Equal(ThemePreference.System, configuration.Settings.Theme);
        Assert.Equal(10, configuration.Settings.LogRetentionRuns);
        Assert.Equal(10 * 1024 * 1024, configuration.Settings.MaxLogBytes);
    }

    [Fact]
    public void Newer_schema_versions_are_rejected()
    {
        var configuration = AppConfiguration.CreateDefault() with { SchemaVersion = 2 };

        Assert.Throws<UnsupportedSchemaVersionException>(() => AppConfigurationValidator.Validate(configuration));
    }
}
