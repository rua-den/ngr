# NGR Launcher

NGR Launcher is a lightweight Windows launcher for reusable applications, commands, and ordered one-click profiles.

## Current status

The project is in active MVP development. The execution/configuration backend and the main Tool/Profile workflows are functional; single-instance activation, final interactive Windows UX smoke testing, source cleanup, and final acceptance are still pending.

### Usable now

- Create reusable **Application** or **Command** tools.
- Application tools use **Choose app…** to show a searchable list of apps exposed through the Windows Start menu and registered App Paths.
- If an application is not found in that list, use **Browse file instead…** as the fallback for an executable, shortcut, or file.
- Working directories use **Choose folder…** instead of requiring a manually typed path.
- Tool IDs are generated automatically from the tool name for new tools.
- Application and Command editors show only fields relevant to the selected type.
- Tool configuration is validated before Save/Test run; rooted file targets and working directories must exist.
- Test-run a tool before saving it.
- Create/edit/delete profiles without manually entering internal IDs.
- Add reusable tools as ordered steps, configure 0–300 second delays, reorder/remove steps, and validate the profile before saving.
- Profiles show a clear empty state when no reusable tools exist yet.
- Dashboard shows a selected-profile summary, clear no-profile/no-run states, run history, step progress, and pending-step cancellation.
- Launch profiles from Dashboard or directly from the tray menu.
- Multiple profile runs may execute concurrently.
- Closing the management window hides it to the system tray when tray registration succeeds.
- Tray menu includes Open, direct profile shortcuts, and Exit, and uses the launcher light/dark resources for readability.
- Explicit Exit confirms first, cancels pending profile steps, and stops managed command process trees.
- **Start with Windows** writes the current-user Windows Run registration instead of only saving a preference.
- First normal launch asks once whether NGR should start with Windows.
- Windows startup uses the `--startup` mode: NGR starts hidden in the tray and never auto-runs a profile.
- If tray registration fails during startup, NGR opens its window instead of leaving an invisible process behind.
- Light, Dark, and System themes are supported, including the native Windows title bar.

### Still incomplete

- Single-instance activation: a second launch should activate the existing NGR process instead of opening another copy.
- Final interactive Windows UX smoke testing of the installed-app picker, tray context menu, registry startup, startup-hidden behavior, tray lifecycle, and managed command shutdown.
- Broader packaged/Store-app discovery may be added if Windows does not expose a particular app through the Start menu or App Paths; the file picker remains the fallback.
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
2. Enter a name such as `Visual Studio Code` and leave the type as **Application**.
3. Click **Choose app…**, search the installed-app list, select the app, and click **Use selected app**. If the app is missing, choose **Browse file instead…** and locate the executable/shortcut manually.
4. Optionally choose a **Working directory** with **Choose folder…**.
5. Click **Test run**. If it launches correctly, click **Save tool**.
6. Open **Profiles**, enter a profile name, click **Add tool**, choose the saved tool, optionally set a delay, and save the profile.
7. Open **Dashboard**, select the profile on the left, and click **Run profile**. Verify each launch step appears in the run history.
8. Close the main window and verify NGR remains in the tray. Right-click the tray icon and verify the menu is readable in the active light/dark theme; use **Open NGR Launcher**, a direct profile shortcut, and **Exit NGR Launcher**.
9. In **Settings**, enable **Start NGR Launcher when I sign in**, save, then verify the current-user Windows startup entry launches NGR hidden in the tray on the next sign-in/startup simulation.

Data is stored under `%LocalAppData%\NGR Launcher`.
