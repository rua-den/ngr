using Microsoft.Win32;
using Ngr.Launcher.Core.Management;

namespace Ngr.Launcher.App.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public static class StartupLaunchArguments
{
    public const string Startup = "--startup";

    public static string BuildCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\" {Startup}";
    }

    public static bool IsStartupLaunch(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Any(argument =>
            string.Equals(argument, Startup, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class WindowsStartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NGR Launcher";
    private readonly string _command;

    public WindowsStartupRegistrationService(string executablePath)
    {
        _command = StartupLaunchArguments.BuildCommand(executablePath);
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return string.Equals(value, _command, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows startup registry key could not be opened.");

        if (enabled)
        {
            key.SetValue(ValueName, _command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

public sealed class StartupOnboardingCoordinator(
    LauncherWorkspace workspace,
    IConfirmationService confirmation,
    IStartupRegistrationService startupRegistration)
{
    private readonly LauncherWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    private readonly IConfirmationService _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
    private readonly IStartupRegistrationService _startupRegistration = startupRegistration ?? throw new ArgumentNullException(nameof(startupRegistration));

    public async Task RunIfNeededAsync(
        bool startupLaunch,
        CancellationToken cancellationToken = default)
    {
        if (startupLaunch || _workspace.Configuration.Settings.StartupPromptAnswered)
        {
            return;
        }

        var enable = _confirmation.Confirm(
            "Start with Windows",
            "Start NGR Launcher automatically when you sign in to Windows? It will start hidden in the system tray and will not run any profile automatically.");
        var previous = _startupRegistration.IsEnabled();

        try
        {
            _startupRegistration.SetEnabled(enable);
            var settings = _workspace.Configuration.Settings with
            {
                StartupPromptAnswered = true,
                StartWithWindows = enable
            };
            await _workspace.UpdateSettingsAsync(settings, cancellationToken);
        }
        catch
        {
            TryRestore(previous);
            throw;
        }
    }

    private void TryRestore(bool enabled)
    {
        try
        {
            _startupRegistration.SetEnabled(enabled);
        }
        catch
        {
            // Preserve the original failure. Settings can be retried from the UI.
        }
    }
}
