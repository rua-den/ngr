# NGR Launcher

NGR Launcher is a lightweight Windows launcher for reusable applications, commands, and ordered one-click profiles.

## Current status

The project is in active MVP development. The execution/configuration backend and the main Tool/Profile workflows are functional; single-instance activation, final Dashboard polish, source cleanup, and full manual acceptance are still pending.

### Usable now

- Create reusable **Application** or **Command** tools.
- Application tools support **Browse…** for executables/files and **Browse folder…** for working directories.
- Tool IDs are generated automatically from the tool name for new tools.
- Application and Command editors show only fields relevant to the selected type.
- Tool configuration is validated before Save/Test run; rooted file targets and working directories must exist.
- Test-run a tool before saving it.
- Create/edit/delete profiles without manually entering internal IDs.
- Add reusable tools as ordered steps, configure 0–300 second delays, reorder/remove steps, and validate the profile before saving.
- Profiles show a clear empty state when no reusable tools exist yet.
- Launch profiles from Dashboard or the tray menu and cancel pending steps.
- Multiple profile runs may execute concurrently.
- Closing the management window hides it to the system tray when tray registration succeeds.
- Tray menu includes Open, profile shortcuts, and Exit.
- Explicit Exit confirms first, cancels pending profile steps, and stops managed command process trees.
- **Start with Windows** writes the current-user Windows Run registration instead of only saving a preference.
- First normal launch asks once whether NGR should start with Windows.
- Windows startup uses the `--startup` mode: NGR starts hidden in the tray and never auto-runs a profile.
- If tray registration fails during startup, NGR opens its window instead of leaving an invisible process behind.
- Light, Dark, and System themes are supported, including the native Windows title bar.

### Still incomplete

- Single-instance activation: a second launch should activate the existing NGR process instead of opening another copy.
- Dashboard usability polish / stronger empty and active-run states.
- Final Windows manual smoke testing of registry startup, startup-hidden behavior, tray lifecycle, second-instance behavior, and managed command shutdown.
- Final source cleanup of temporary `.Fixed.cs` / `.Corrected.cs` compatibility files and MSBuild exclusions.
- Final whole-solution review and plan/status cleanup.

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
6. Open **Profiles**, enter a profile name, click **Add tool**, choose the saved tool, optionally set a delay, and save the profile.
7. Open **Dashboard**, select the profile, and click **Run profile**.
8. Close the main window and verify NGR remains in the tray; use tray **Open** to restore it and **Exit** to fully stop NGR.
9. In **Settings**, enable **Start NGR Launcher with Windows**, save, then verify the current-user Windows startup entry launches NGR hidden in the tray on the next sign-in/startup simulation.

Data is stored under `%LocalAppData%\NGR Launcher`.
