# NGR Launcher implementation plan

## Product boundary

NGR Launcher is a lightweight Windows 10/11 desktop launcher for reusable tools and ordered one-click profiles. It can start with Windows, normally lives in the notification area, and opens a WPF management window backed by local JSON configuration.

The MVP supports two reusable tool kinds:

- Application: executable, document, shortcut, URI, or shell-associated target plus optional arguments and working directory.
- Command: Command Prompt or Windows PowerShell command, hidden or in a visible terminal, plus working directory and environment overrides.

A profile stores references to tools rather than copies. Its steps run in order, each with a delay before launch. One failed step is reported and the remaining steps continue. A profile may be launched concurrently with itself; every launch uses an immutable snapshot and receives a distinct session ID.

Cancel stops only pending profile steps. Commands that already started remain managed until they exit. When the user explicitly exits NGR Launcher, the app confirms first, cancels pending sessions, and kills the complete process tree of every managed command. Desktop applications are never killed by NGR Launcher.

## Technology and distribution

- .NET 10 LTS, WPF, MVVM.
- WPF-UI 4.3.0, WPF-UI.Tray 4.3.0, CommunityToolkit.Mvvm 8.4.0.
- English UI.
- Unpackaged, framework-dependent executable for MVP.
- GitHub `main` is the current source of truth; implementation work is committed and pushed incrementally.
- Data under `%LocalAppData%\NGR Launcher`.

## Completion policy

A phase item is not considered product-complete merely because code exists or CI compiles. For user-facing work, completion means the intended workflow can be performed end-to-end from the UI without editing JSON, manually inventing IDs, or knowing internal file paths that the UI can discover for the user. Each implementation batch should pass Windows Debug tests and Release build before moving on. Interactive desktop-only behavior remains pending until manually smoke-tested on Windows.

## Phase 1 — foundation and configuration

1. Create solution, WPF app, core library, xUnit tests, and test child process. **Implemented.**
2. Define tool, profile, step, settings, and root configuration models. **Implemented.**
3. Validate required values, enum values, duplicate IDs, tool-kind-specific fields, profile references, and non-negative delays. **Implemented.**
4. Serialize enums as strings and reject numeric enum values. **Implemented.**
5. Save configuration atomically with a backup. Recover from malformed, null, unsupported, or invalid primary data by restoring backup or defaults. **Implemented.**
6. Verify Debug tests and Release build with zero warnings. **Passing in current Windows CI.**
7. Independent review. **Still pending; no independent review should be claimed until one is actually run.**

Checkpoint: backend foundation is implemented and exercised by tests. Final source consolidation/cleanup remains part of the final acceptance gate.

## Phase 2 — execution engine

1. Build exact `ProcessStartInfo`-style launch specifications for applications, Command Prompt, and Windows PowerShell in hidden and terminal modes. **Implemented.**
2. Snapshot and run ordered profiles with delay, progress, cancellation of pending steps, failure continuation, summaries, and concurrent sessions. **Implemented.**
3. Track duplicate live command instances by tool ID; remove them on exit. **Implemented.**
4. Capture hidden stdout/stderr with UTC timestamps, a 10 MiB content cap, one truncation marker, and retention of the latest ten logs per tool. **Implemented.**
5. Implement the real system process adapter and `Kill(entireProcessTree: true)`. **Implemented.**
6. Use integration tests to prove real hidden output capture, retention, and descendant termination. **Implemented; the process-tree test is known to occasionally time out on hosted Windows and must be treated as a test flake only when a rerun passes without code changes.**
7. Verify Debug tests and Release build. **Passing in current Windows CI.** Independent review remains pending.

Checkpoint: execution implementation is functionally complete for the MVP and verified by the current automated suite. Remaining work is review/final cleanup, not feature implementation.

## Phase 3 — management UI and user workflow

1. Navigation shell for Dashboard, Tool Library, Profiles, and Settings. **Implemented as a VS Code-style workbench shell.**
2. Tool Library: list, add, edit, delete, validate, and launch a single tool. **Implemented and under current UX stabilization.**
3. Installed-app discovery for Application tools. **Added by product decision after user testing:** `Choose app…` discovers Start Menu shortcuts and registered Windows App Paths, shows Windows Shell icons/friendly names, and falls back to `Browse file instead…`. The catalog is refreshed whenever the picker opens.
4. Templates: generic application, generic CLI, Microsoft Teams, Avrea CLI, ngrok, and npm; templates prefill editable fields and do not install software. **Implemented.**
5. Tool usability: file/folder pickers, auto-generated internal IDs, type-specific fields, validation, Test run, visible dirty state, and discard protection. **Implemented; interactive Windows smoke pending.**
6. Profiles: list, create, edit, delete, reorder steps, choose reusable tools, and configure per-step delays. **Implemented.**
7. Profile usability: internal IDs hidden, explicit choose-tool → Add tool flow, ordered step editor, empty states, dirty state, and discard protection. **Implemented; interactive Windows smoke pending.**
8. Dashboard: launch a profile once, show current/pending/started/failed/cancelled state, cancel pending steps, and allow concurrent launches. **Implemented.**
9. Use confirmation dialogs for destructive configuration changes and for discarding unsaved Tool/Profile edits. **Implemented.**
10. Persist saved edits through the atomic configuration store while preserving unsaved editors across unrelated workspace changes. **Implemented and covered by view-model tests.**
11. Add view-model tests before/with production behavior. **Implemented and passing in Windows CI.** Independent review remains pending.

Current Phase 3 gate: automated verification is green. Do not mark the UI/UX stabilization stage finished until the current manual Windows smoke checklist in README passes, especially installed-app icons/friendly names, unsaved-change prompts, profile step editing, Light/Dark readability, and Tool → Profile → Dashboard end-to-end usage.

## Phase 4 — Windows lifecycle

1. Closing the management window hides it to the notification area. **Implemented; manual smoke pending.**
2. Tray menu: Open, direct profile shortcuts, and Exit. **Implemented; light/dark visual smoke pending.**
3. Exit always shows the end-user confirmation that managed command trees will stop; confirmation triggers session cancellation and `StopAll`. **Implemented and tested.**
4. Enforce a single app instance with a named mutex and named-pipe activation. A second launch activates the first instance instead of opening another. **Not implemented yet; this is the next major feature after the current UX stabilization gate.**
5. First-run onboarding offers to enable Windows startup. Startup launches hidden and never auto-runs a profile. **Implemented; manual Windows smoke pending.**
6. Settings can enable/disable the actual current-user Windows startup entry and choose theme. **Implemented.**
7. Lifecycle tests and independent review. **Lifecycle tests are partial; independent review remains pending.**

## Explicitly outside current MVP

- Installing or updating third-party tools.
- Guaranteeing exhaustive discovery of every packaged/Store application. Current discovery uses Start Menu entries and App Paths; `Browse file instead…` is the fallback.
- Health checks, automatic restart, and background supervision.
- Streaming live-log viewer.
- Cloud sync, accounts, teams, telemetry, import/export, or an installer.
- Automatically running a profile at Windows startup.

## Current stabilization gate

Before starting single-instance work, the current UI/UX stage must satisfy all of the following:

1. Windows Debug tests pass.
2. Windows Release build passes with warnings treated as errors.
3. Tool Library can discover a normal installed application with icon and friendly name, save it, and Test run it without manual ID entry.
4. `Browse file instead…` and `Choose folder…` work as fallbacks where appropriate.
5. Unsaved Tool edits are not silently lost when selecting another tool, creating a new tool, applying a template, or when unrelated workspace data changes.
6. Profile editor uses explicit tool selection before Add, allows delay/reorder/remove, and protects unsaved edits.
7. Internal IDs are not exposed in the normal user workflow.
8. Dashboard runs the saved profile and displays understandable run/step state.
9. Launcher/window/taskbar/tray icons are present; installed-app picker entries are visually distinguishable by app icon.
10. Tray menu and management UI remain readable in Light and Dark themes.
11. The manual checklist in README is completed on a real Windows desktop session.

Automated items 1–2 currently pass on commit `0081337`; items 3–11 require/benefit from interactive Windows smoke testing before the stage is called finished.

## Final acceptance gate

1. Implement and verify single-instance activation.
2. Remove all temporary `.Fixed.cs`, `.Corrected.cs`, compatibility shims, draft tests, and MSBuild exclusions by consolidating each final source file.
3. Run the full Debug test suite, real process integration tests, and Release build with zero warnings/errors.
4. Manually smoke-test tool CRUD, app discovery/fallback, unsaved-change protection, profile CRUD/order/delay, repeated profile launch, tray hide/open, startup toggle, second-instance activation, and confirmed Exit killing command trees but not desktop Applications.
5. Run a final whole-solution review and resolve critical/important findings.
6. Update README and this plan so no stale implementation status remains.
7. Commit and push the accepted state to GitHub `main` under the current project workflow.
