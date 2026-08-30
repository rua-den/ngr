using Ngr.Launcher.Core.Execution;
using Ngr.Launcher.Core.Models;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ShellCommandBuilderBehaviorTests
{
    [Theory]
    [InlineData(CommandWindowMode.Hidden, "/d /s /c echo hello")]
    [InlineData(CommandWindowMode.Terminal, "/d /s /k echo hello")]
    public void Command_prompt_uses_expected_lifetime_switch(
        CommandWindowMode windowMode,
        string expectedArguments)
    {
        var command = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.CommandPrompt,
            windowMode,
            "echo hello"));

        Assert.Equal("cmd.exe", command.FileName);
        Assert.Equal(expectedArguments, command.Arguments);
    }

    [Fact]
    public void PowerShell_uses_expected_hidden_and_terminal_switches()
    {
        var hidden = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.WindowsPowerShell,
            CommandWindowMode.Hidden,
            "Get-Date"));
        var terminal = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.WindowsPowerShell,
            CommandWindowMode.Terminal,
            "Get-Date"));

        Assert.Equal("powershell.exe", hidden.FileName);
        Assert.Equal("-NoLogo -NoProfile -NonInteractive -Command Get-Date", hidden.Arguments);
        Assert.Equal("-NoLogo -NoProfile -NoExit -Command Get-Date", terminal.Arguments);
    }

    [Fact]
    public void Defaults_working_directory_and_applies_environment_overrides()
    {
        var command = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.CommandPrompt,
            CommandWindowMode.Hidden,
            "set",
            EnvironmentVariables: new Dictionary<string, string>
            {
                ["PATH"] = "custom-path",
                ["NGR_TEST_ONLY"] = "custom-value"
            }));

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), command.WorkingDirectory);
        Assert.Equal("custom-path", command.Environment["PATH"]);
        Assert.Equal("custom-value", command.Environment["NGR_TEST_ONLY"]);
    }

    [Fact]
    public void Application_request_uses_shell_and_preserves_values()
    {
        var request = new ApplicationLaunchRequest("notepad.exe", "file.txt", @"C:\work");

        var command = ShellCommandBuilder.BuildApplication(request);

        Assert.True(command.UseShellExecute);
        Assert.Equal(request.Target, command.FileName);
        Assert.Equal(request.Arguments, command.Arguments);
        Assert.Equal(request.WorkingDirectory, command.WorkingDirectory);
    }
}
