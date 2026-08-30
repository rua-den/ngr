# NGR Launcher

NGR Launcher is a lightweight Windows launcher for reusable applications, commands, and ordered one-click profiles.

## Current status

The project is in active MVP development. The execution/configuration backend is functional, while the desktop management workflows are still being completed and manually smoke-tested.

### Usable now

- Create reusable **Application** or **Command** tools.
- Application tools support **Browse…** for executables/files and **Browse folder…** for working directories.
- Tool IDs are generated automatically from the tool name for new tools.
- Application and Command editors show only the fields relevant to the selected type.
- Tool configuration is validated before Save/Test run; rooted file targets and working directories must exist.
- Test-run a tool before saving it.
- Create/edit/delete profiles, add reusable tools as ordered steps, configure delays, and reorder steps.
- Launch profiles from Dashboard or the tray menu and cancel pending steps.
- Multiple profile runs may execute concurrently.
- Closing the management window hides it to the system tray when tray registration succeeds.
- Tray menu includes Open, profile shortcuts, and Exit.
- Explicit Exit confirms first, cancels pending profile steps, and stops managed command process trees.
- Light, Dark, and System themes are supported, including the native Windows title bar.

### Still incomplete

- Start-with-Windows registry integration.
- First-run onboarding and startup-hidden behavior.
- Single-instance activation.
- Profile editor usability polish (automatic IDs / clearer step workflow / empty states).
- Dashboard usability polish and final manual smoke testing.
- Final source cleanup of temporary `.Fixed.cs` / `.Corrected.cs` compatibility files.

## Requirements

- Windows 10 or Windows 11
- .NET 10 SDK for development

## Build

```powershell
dotnet restore Ngr.Launcher.sln
dotnet build Ngr.Launcher.sln -c Release
```

## Run

```powershell
dotnet run --project src/Ngr.Launcher.App/Ngr.Launcher.App.csproj
```

## Test

```powershell
dotnet test Ngr.Launcher.sln -c Debug
```

## Quick manual test

1. Open **Tool Library** and choose **New tool**.
2. Enter a name such as `Visual Studio Code`.
3. Leave type as **Application**, click **Browse…**, and choose the executable/file you want NGR to launch.
4. Optionally choose a **Working directory** with **Browse folder…**.
5. Click **Test run**. If it launches correctly, click **Save tool**.
6. Open **Profiles**, create a profile, add the saved tool as a step, then save the profile.
7. Open **Dashboard**, select the profile, and click **Run profile**.
8. Close the main window and verify NGR remains in the tray; use tray **Open** to restore it and **Exit** to fully stop NGR.

Data is stored under `%LocalAppData%\NGR Launcher`.
