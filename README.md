# NGR Launcher

NGR Launcher is a lightweight Windows 10/11 desktop launcher for reusable tools and ordered one-click profiles.

## Current status

The app is usable for manual testing now. The current `main` branch includes:

- Dashboard, Tool Library, Profiles, and Settings management UI.
- Reusable Application and Command tools.
- Tool templates and single-tool launch.
- Ordered profiles with per-step delays.
- Concurrent profile launches and cancellation of pending steps.
- Managed hidden-command logging and process-tree shutdown.
- System / Light / Dark themes.
- Closing the main window hides it to the system tray.
- Tray menu with Open, profile shortcuts, and Exit.
- Safe Exit confirmation that cancels pending sessions and stops all managed command process trees while leaving launched desktop applications running.

Still planned before the MVP is complete:

- Real Windows startup registration / Settings integration.
- First-run startup onboarding and startup-hidden behavior.
- Single-instance enforcement with activation of the existing instance.
- Final source cleanup, full review, and final manual acceptance pass.

## Requirements

- Windows 10 or Windows 11.
- .NET 10 SDK.

Check your SDK with:

```powershell
dotnet --version
```

## Run locally

Clone the repository and run the WPF app:

```powershell
git clone https://github.com/rua-den/ngr.git
cd ngr
dotnet restore Ngr.Launcher.sln
dotnet run --project src/Ngr.Launcher.App/Ngr.Launcher.App.csproj
```

The launcher stores its configuration and logs under:

```text
%LocalAppData%\NGR Launcher
```

Delete that directory only if you intentionally want a clean local configuration while testing.

## Suggested manual smoke test

1. Open **Tool Library** and create an Application or Command tool.
2. Launch the tool directly and verify it starts correctly.
3. Create a profile with multiple tools, reorder the steps, and add delays.
4. Launch the profile from **Dashboard** and verify step status updates.
5. Launch the same profile again while the first run is active to verify concurrent sessions.
6. Start a delayed profile and cancel it; already-started commands should remain running while pending steps are cancelled.
7. Change the theme between System, Light, and Dark, save, restart the app, and verify the theme persists.
8. Click the window **X** button; the window should hide instead of exiting.
9. Left-click the tray icon to restore the window.
10. Right-click the tray icon and verify **Open**, **Profiles**, and **Exit**.
11. Launch a profile from the tray profile shortcut.
12. Start a managed command, choose **Exit**, decline once, then confirm. Confirmed exit should stop managed command process trees but must not terminate ordinary desktop applications launched by NGR Launcher.

Known manual-test limitation: Windows startup and second-instance activation are not implemented yet, so do not test those as completed features.

## Build

```powershell
dotnet build Ngr.Launcher.sln -c Release
```

## Test

```powershell
dotnet test Ngr.Launcher.sln -c Debug
```

The GitHub Actions Windows workflow also runs the Debug test suite and a Release build on pushes to `main`.

## Implementation plan

The working implementation plan is in:

```text
docs/plans/2026-08-30-ngr-launcher-implementation.md
```
