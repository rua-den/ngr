using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests;

public sealed class DomainValidationTests
{
    [Fact]
    public void Application_requires_target_and_rejects_command_only_fields()
    {
        var missingTarget = new ToolDefinition
        {
            Id = "app-1",
            Name = "App",
            Kind = ToolKind.Application,
            Target = ""
        };

        var commandFields = new ToolDefinition
        {
            Id = "app-2",
            Name = "App",
            Kind = ToolKind.Application,
            Target = @"C:\\Tools\\app.exe",
            CommandText = "should not be set",
            Shell = ShellKind.CommandPrompt,
            WindowMode = CommandWindowMode.Hidden
        };

        Assert.Contains(ToolValidationError.TargetRequired, ToolValidator.Validate(missingTarget));
        Assert.Contains(ToolValidationError.IncompatibleField, ToolValidator.Validate(commandFields));
    }

    [Fact]
    public void Command_requires_command_text_and_valid_shell_fields()
    {
        var tool = new ToolDefinition
        {
            Id = "cmd-1",
            Name = "Command",
            Kind = ToolKind.Command,
            Shell = ShellKind.WindowsPowerShell,
            WindowMode = CommandWindowMode.Terminal,
            CommandText = ""
        };

        var errors = ToolValidator.Validate(tool);

        Assert.Contains(ToolValidationError.CommandTextRequired, errors);
        Assert.DoesNotContain(ToolValidationError.TargetRequired, errors);
    }

    [Theory]
    [InlineData("not a valid env key")]
    [InlineData("1INVALID")]
    [InlineData("A=B")]
    public void Environment_variable_keys_must_be_valid(string key)
    {
        var tool = ValidCommand() with
        {
            EnvironmentVariables = new Dictionary<string, string> { [key] = "value" }
        };

        Assert.Contains(ToolValidationError.EnvironmentKeyInvalid, ToolValidator.Validate(tool));
    }

    [Fact]
    public void Application_target_accepts_path_or_uri_but_rejects_missing_target()
    {
        var uri = ValidApplication() with { Target = "https://example.test" };
        var malformed = ValidApplication() with { Target = "   " };

        Assert.Empty(ToolValidator.Validate(uri));
        Assert.Contains(ToolValidationError.TargetRequired, ToolValidator.Validate(malformed));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(301)]
    public void Profile_step_delay_is_limited_to_zero_through_three_hundred(int seconds)
    {
        var profile = new ProfileDefinition
        {
            Id = "profile-1",
            Name = "Profile",
            Steps = [new ProfileStep { ToolId = "tool-1", DelayBeforeSeconds = seconds }]
        };

        Assert.Contains(ProfileValidationError.DelayOutOfRange, ProfileValidator.Validate(profile, [ValidApplication()]));
    }

    [Fact]
    public void Profile_requires_name_and_at_least_one_step()
    {
        var profile = new ProfileDefinition { Id = "profile-1", Name = "", Steps = [] };

        var errors = ProfileValidator.Validate(profile, []);

        Assert.Contains(ProfileValidationError.NameRequired, errors);
        Assert.Contains(ProfileValidationError.StepsRequired, errors);
    }

    [Fact]
    public void Profile_rejects_unknown_tools_and_preserves_step_order()
    {
        var profile = new ProfileDefinition
        {
            Id = "profile-1",
            Name = "Ordered",
            Steps =
            [
                new ProfileStep { ToolId = "second", DelayBeforeSeconds = 2 },
                new ProfileStep { ToolId = "missing", DelayBeforeSeconds = 0 },
                new ProfileStep { ToolId = "first", DelayBeforeSeconds = 1 }
            ]
        };

        var errors = ProfileValidator.Validate(profile, [ValidApplication() with { Id = "first" }, ValidApplication() with { Id = "second" }]);

        Assert.Contains(ProfileValidationError.ToolNotFound, errors);
        Assert.Equal(["second", "missing", "first"], profile.Steps.Select(step => step.ToolId));
    }

    private static ToolDefinition ValidApplication() => new()
    {
        Id = "tool-1", Name = "Application", Kind = ToolKind.Application, Target = @"C:\\Tools\\app.exe"
    };

    private static ToolDefinition ValidCommand() => new()
    {
        Id = "tool-1", Name = "Command", Kind = ToolKind.Command,
        Shell = ShellKind.CommandPrompt, WindowMode = CommandWindowMode.Hidden,
        CommandText = "echo hello"
    };
}
