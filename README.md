# NGR Launcher

NGR Launcher is a lightweight Windows launcher for reusable applications, commands, and ordered one-click profiles.

## Current status

The project is in active MVP development. The execution/configuration backend and the core Tool → Profile → Dashboard workflow are functional. The current UI/UX stabilization stage has passed Windows Debug tests and Release build; interactive Windows smoke testing is still required before this stage is called finished. Single-instance activation, source cleanup, and final acceptance remain after that.

### Usable now

- Create reusable **Application** or **Command** tools.
- Application tools use **Choose app…** to open a searchable installed-app picker before asking for a file path.
- The installed-app picker scans the current Windows Start menu and registered App Paths each time it opens, so newly installed/uninstalled apps do not require an NGR restart to refresh the list.
- Installed apps show their Windows Shell icon, friendly app name, and source. Selecting an app such as Google Chrome uses the friendly name instead of exposing `chrome.exe` as the default tool name.
- If an application is not exposed by Windows in that list, use **Browse file instead…** as the fallback for an executable, shortcut, or file.
- Working directories use **Choose folder…** instead of requiring a manually typed path.
- Tool and profile internal IDs are generated automatically and are no longer shown in the normal UI.
- Tool sidebar entries show an icon, human-readable name, and Application/Command type.
- Application and Command editors show only fields relevant to the selected type.
- Tool configuration is validated before Save/Test run; rooted file targets and working directories must exist.
- **Test run** a tool before saving it.
- Unsaved Tool changes are visibly marked. Switching tool, starting a new tool, or applying a template asks before discarding unsaved edits.
- Create/edit/delete profiles without manually entering internal IDs.
- Profile creation uses an explicit **choose tool → Add tool → delay/reorder** workflow instead of silently inserting the first available tool.
- Profile steps are shown as an ordered editable list with a tool selector and delay in seconds.
- Unsaved Profile changes are visibly marked and protected from accidental navigation or unrelated workspace refreshes.
- Dashboard shows a selected-profile summary, clear no-profile/no-run states, run history, step progress, and pending-step cancellation.
- Launch profiles from Dashboard or directly from the tray menu.
- Multiple profile runs may execute concurrently.
- Closing the management window hides it to the system tray when tray registration succeeds.
- The executable, main window/taskbar, and tray use the NGR launcher icon.
- Tray menu includes Open, direct profile shortcuts, and Exit, and uses the launcher light/dark resources for readability.
- Explicit Exit confirms first, cancels pending profile steps, and stops managed command process trees while leaving desktop Applications running.
- **Start with Windows** writes the current-user Windows Run registration instead of only saving a preference.
- First normal launch asks once whether NGR should start with Windows.
- Windows startup uses the `--startup` mode: NGR starts hidden in the tray and never auto-runs a profile.
- If tray registration fails during startup, NGR opens its window instead of leaving an invisible process behind.
- Light, Dark, and System themes are supported, including the native Windows title bar.

### Still incomplete

- **Interactive Windows UX smoke for this stabilization stage**: verify real installed-app icons/friendly names, unsaved-change prompts, profile step editing, dark/light tray menu, taskbar/tray icon, and the complete Tool → Profile → Dashboard workflow on a desktop session.
- Single-instance activation: a second launch should activate the existing NGR process instead of opening another copy.
- Broader packaged/Store-app discovery may be added if Windows does not expose a particular app through the Start menu or App Paths; **Browse file instead…** remains the fallback.
- Final Windows lifecycle smoke testing of registry startup, startup-hidden behavior, tray lifecycle, second-instance behavior, and managed command shutdown.
- Final source cleanup of temporary `.Fixed.cs` / `.Corrected.cs` compatibility files and MSBuild exclusions.
- Final whole-solution review and acceptance cleanup.

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

## Current stabilization smoke test

1. Fully exit any older NGR process from the tray, pull `main`, then run NGR.
2. Verify the NGR icon appears on the window/taskbar and on the tray icon.
3. Open **Tool Library** → **New tool** → **Choose app…**. Confirm the list has recognizable app icons and friendly names; search for an installed app such as Chrome or VS Code.
4. Select an app and confirm the Tool editor receives the friendly app name. Use **Browse file instead…** only for an app/file that is not present in the installed-app list.
5. Optionally choose a **Working directory**, then use **Test run** and **Save changes**.
6. Edit the saved Tool name or target, then click another saved Tool. Choose **No** on the discard prompt and verify the unsaved editor remains intact; choose **Yes** afterward and verify navigation proceeds.
7. Open **Profiles**, enter a profile name, choose a saved tool in the add picker, click **Add tool**, add more tools if desired, edit delays, reorder the selected step, and save the profile.
8. Make an unsaved Profile edit, select another saved Profile, decline the discard prompt, and verify the current edit is preserved.
9. Open **Dashboard**, select the profile in the sidebar, click **Run profile**, and verify each step appears in run history with status/delay/error information.
10. Switch Light/Dark/System themes and verify the title bar, workbench surfaces, inputs, and text remain readable.
11. Close the main window and verify NGR remains in the tray. Right-click the tray icon and verify the context menu is readable in both Light and Dark; test **Open NGR Launcher**, a direct profile shortcut, and **Exit NGR Launcher**.
12. In **Settings**, enable **Start NGR Launcher when I sign in**, save, and verify the current-user Windows startup entry launches NGR hidden in the tray without automatically running a profile.

Data is stored under `%LocalAppData%\NGR Launcher`.
