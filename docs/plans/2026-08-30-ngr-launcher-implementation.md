# NGR Launcher implementation plan

## Product boundary

NGR Launcher is a lightweight Windows 10/11 desktop launcher for reusable
tools and ordered one-click profiles. It starts with Windows, normally lives in
the notification area, and opens a WPF management window backed by local JSON
configuration.

The MVP supports two reusable tool kinds:

- Application: executable, document, URI, or shell-associated target plus
  optional arguments and working directory.
- Command: Command Prompt or Windows PowerShell command, hidden or in a visible
  terminal, plus working directory and environment overrides.

A profile stores references to tools rather than copies. Its steps run in order,
each with a delay before launch. One failed step is reported and the remaining
steps continue. A profile may be launched concurrently with itself; every launch
uses an immutable snapshot and receives a distinct session ID.

Cancel stops only pending profile steps. Commands that already started remain
managed until they exit. When the user explicitly exits NGR Launcher, the app
confirms first, cancels pending sessions, and kills the complete process tree of
every managed command. Desktop applications are never killed by NGR Launcher.

## Technology and distribution

- .NET 10 LTS, WPF, MVVM.
- WPF-UI 4.3.0, WPF-UI.Tray 4.3.0, CommunityToolkit.Mvvm 8.4.0.
- English UI.
- Unpackaged, framework-dependent executable for MVP.
- Local Git repository; user handles the remote and push.
- Data under `%LocalAppData%\NGR Launcher`.

## Phase 1 — foundation and configuration

1. Create solution, WPF app, core library, xUnit tests, and test child process.
2. Define tool, profile, step, settings, and root configuration models.
3. Validate required values, enum values, duplicate IDs, tool-kind-specific
   fields, profile references, and non-negative delays.
4. Serialize enums as strings and reject numeric enum values.
5. Save configuration atomically with a backup. Recover from malformed, null,
   unsupported, or invalid primary data by restoring backup or defaults.
6. Verify Debug tests and Release build with zero warnings.
7. Run an independent Luna review and close all important findings.

Checkpoint: completed and independently reviewed. Temporary superseded-source
exclusions remain until the Windows workspace helper can update/delete files.

## Phase 2 — execution engine

1. Build exact `ProcessStartInfo`-style launch specifications for applications,
   Command Prompt, and Windows PowerShell in hidden and terminal modes.
2. Snapshot and run ordered profiles with delay, progress, cancellation of
   pending steps, failure continuation, summaries, and concurrent sessions.
3. Track duplicate live command instances by tool ID; remove them on exit.
4. Capture hidden stdout/stderr with UTC timestamps, a 10 MiB content cap, one
   truncation marker, and retention of the latest ten logs per tool.
5. Implement the real system process adapter and `Kill(entireProcessTree: true)`.
6. Use integration tests to prove real hidden output capture, retention, and
   descendant termination.
7. Verify Debug tests and Release build, then run an independent Luna review.

Checkpoint status: in progress. Unit contracts and replacement implementation
exist; integration RED tests exist. Fresh RED/GREEN execution is pending because
the local command helper is unavailable and the escalated runner reached its
Codex usage reset.

## Phase 3 — management UI

1. Build a navigation shell for Dashboard, Tool Library, Profiles, and Settings.
2. Tool Library: list, add, edit, delete, validate, and launch a single tool.
3. Templates: generic application, generic CLI, Microsoft Teams, aevra, ngrok,
   and npm; templates prefill editable fields and do not install software.
4. Profiles: list, create, edit, delete, reorder steps, choose reusable tools,
   and configure per-step delays.
5. Dashboard: launch a profile once, show current/pending/started/failed/cancelled
   state, cancel pending steps, and allow concurrent launches.
6. Use confirmation dialogs for destructive configuration changes.
7. Persist edits immediately through the atomic configuration store.
8. Add view-model tests before production behavior, then run an independent Luna
   review.

## Phase 4 — Windows lifecycle

1. Closing the management window hides it to the notification area.
2. Tray menu: Open, profile shortcuts, and Exit.
3. Exit always shows the end-user confirmation that managed command trees will
   stop; confirmation triggers session cancellation and `StopAll`.
4. Enforce a single app instance with a named mutex and named-pipe activation.
   A second launch activates the first instance instead of opening another.
5. First-run onboarding offers to enable Windows startup. Startup launches hidden
   and never auto-runs a profile.
6. Settings can enable/disable startup and choose theme.
7. Add lifecycle tests and run an independent Luna review.

## Explicitly outside MVP

- Auto-detecting installed applications.
- Installing or updating third-party tools.
- Health checks, automatic restart, and background supervision.
- Streaming live-log viewer.
- Cloud sync, accounts, teams, telemetry, import/export, or an installer.
- Automatically running a profile at Windows startup.

## Final acceptance gate

1. Remove all temporary `.Fixed.cs`, compatibility shims, draft tests, and
   MSBuild exclusions by consolidating each final source file.
2. Run the full Debug test suite, real process integration tests, and Release
   build with zero warnings/errors.
3. Manually smoke-test tool CRUD, profile CRUD/order/delay, repeated profile
   launch, tray hide/open, startup toggle, second-instance activation, and
   confirmed exit killing command trees but not applications.
4. Run a final whole-solution review and resolve critical/important findings.
5. Commit locally with no push; hand the repository and verification evidence to
   the user.
