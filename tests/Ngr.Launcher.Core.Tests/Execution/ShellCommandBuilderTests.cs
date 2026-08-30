using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class ShellCommandBuilderTests
{
    [Fact]
    public void CommandPrompt_hidden_uses_one_shot_switch()
    {
        var command = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.CommandPrompt, ShellWindowMode.Hidden, "echo hello"));

        Assert.Equal("cmd.exe", command.FileName);
        Assert.Equal("/d /s /c echo hello", command.Arguments);
    }

    [Fact]
    public void CommandPrompt_terminal_keeps_shell_open()
    {
        var command = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.CommandPrompt, ShellWindowMode.Terminal, "echo hello"));

        Assert.Equal("cmd.exe", command.FileName);
        Assert.Equal("/d /s /k echo hello", command.Arguments);
    }

    [Fact]
    public void WindowsPowerShell_hidden_and_terminal_use_expected_lifetime_switches()
    {
        var hidden = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.WindowsPowerShell, ShellWindowMode.Hidden, "Get-Date"));
        var terminal = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.WindowsPowerShell, ShellWindowMode.Terminal, "Get-Date"));

        Assert.Equal("powershell.exe", hidden.FileName);
        Assert.Equal("-NoLogo -NoProfile -NonInteractive -Command Get-Date", hidden.Arguments);
        Assert.Equal("-NoLogo -NoProfile -NoExit -Command Get-Date", terminal.Arguments);
    }

    [Fact]
    public void Working_directory_defaults_to_user_profile_and_custom_environment_overrides_inherited()
    {
        var command = ShellCommandBuilder.Build(new ShellCommandRequest(
            ShellKind.CommandPrompt, ShellWindowMode.Hidden, "set", EnvironmentVariables: new Dictionary<string, string>
            {
                ["PATH"] = "custom-path",
                ["NGR_TEST_ONLY"] = "custom-value"
            }));

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), command.WorkingDirectory);
        Assert.Equal("custom-path", command.Environment["PATH"]);
        Assert.Equal("custom-value", command.Environment["NGR_TEST_ONLY"]);
    }

    [Fact]
    public void Application_launch_request_uses_shell_and_preserves_target_arguments_and_working_directory()
    {
        var request = new ApplicationLaunchRequest("notepad.exe", "file.txt", "C:\\work");

        var command = ShellCommandBuilder.BuildApplication(request);

        Assert.True(command.UseShellExecute);
        Assert.Equal(request.Target, command.Target);
        Assert.Equal(request.Arguments, command.Arguments);
        Assert.Equal(request.WorkingDirectory, command.WorkingDirectory);
    }
}
