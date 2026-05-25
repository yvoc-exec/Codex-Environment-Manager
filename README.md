# Codex Environment Manager

Codex Environment Manager is a Windows app for managing multiple OpenAI Codex accounts,
profiles, and workspaces on one machine.

It supports two companion modes:

- **Desktop**: a single global Codex desktop instance, launched through a managed account
  profile and workspace binding
- **CLI Companion**: parallel `codex --cd <workspace>` sessions in separate terminal tabs/windows

## What the app manages

- **Accounts**
  - Plus / OAuth accounts
  - API-key accounts
- **Profiles**
  - Generated profile instruction files per persona
  - Per-account `config.toml` profile wiring
- **Workspaces**
  - Local project folders
  - Last-used account/profile/launch type for each workspace
- **Diagnostics**
  - Launch plans
  - Active context files
  - Session snapshots
  - Generated file viewer for account-side Codex files

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- OpenAI Codex Desktop installed, or Codex CLI installed
- [Windows Terminal](https://aka.ms/terminal) if you want tabbed CLI companions
- Git if you want the Git Guard preflight check

## Build and run

```powershell
cd CodexEnvironmentManager
dotnet restore
dotnet build
dotnet run
```

You can also open the solution in Visual Studio 2022+.

## First-run setup

The first-run wizard can:

- auto-detect Codex Desktop availability
- detect Codex CLI availability
- import an existing `.codex` folder into a managed account
- complete setup even if a Desktop executable is not available, as long as your available
  fallback conditions are acceptable

Typical first-run flow:

1. Launch the app.
2. Review the Desktop / CLI detection status.
3. Optionally import an existing `.codex` folder.
4. Finish setup, then add any missing accounts, profiles, or workspaces from the main window.

## Importing an existing `.codex`

The first-run wizard can copy an existing `.codex` folder into a new managed account profile.
This imports the visible contents into CEM-managed storage so the account can be used by the app.

## Adding accounts

Use the account buttons in the main window:

- **+ Plus**: creates a Plus / OAuth-backed account entry
- **+ API**: creates an API-key account entry

Notes:

- API keys are stored using Windows DPAPI at rest
- OAuth-backed accounts keep their own isolated `auth.json` and profile files under the managed
  account home

## Adding workspaces

Add a workspace by selecting a local project folder and giving it a name.

Workspaces are used to:

- remember the last account/profile used with that folder
- restore the last launch type with **Resume Last**
- drive Desktop workspace binding and CLI `--cd` launches

## Launch contract confirmation

Before any launch, CEM shows a **Verify Launch Contract** prompt.

This preview is meant to confirm:

- the selected account
- the selected profile
- the selected workspace
- the launch type you are about to run

You must confirm the prompt before the launch proceeds.

## Launch Desktop

Desktop launch will:

1. preflight the selected account/profile/workspace
2. write the active context and launch plan artifacts
3. verify the launch contract
4. close any existing tracked Desktop sessions only after the target can be verified
5. swap the `.codex` junction to the selected account
6. launch Codex Desktop

If a stale `desktop_store` placeholder is found during preflight and no live Desktop instance can be
proven, CEM may retire that placeholder instead of blocking the relaunch.

Desktop diagnostics include:

- `last_launch_plan.json`
- `last_desktop_launch_plan.json`
- `last_desktop_deeplinks.txt`

### Desktop workspace binding

For the Store/AppX / `codex app` path, workspace binding is best-effort and diagnostic.
The app records the deep-link variants and activation attempts, but it does not claim that the
workspace binding is fully proven unless the code can verify it.

## Launch CLI Companion

CLI Companion launch:

- does **not** swap the global `.codex` junction
- scopes `CODEX_HOME` to the selected account
- launches Codex in a terminal session
- uses Windows Terminal if enabled in settings, otherwise falls back to `cmd.exe`

This is the most reliable way to run multiple parallel Codex sessions against different workspaces.

## View Files

The **View Files** button opens the generated account-side files for the selected account/profile:

- `config.toml`
- `AGENTS.md`
- `cem-profiles/<profile>.instructions.md`

This is a quick way to inspect what CEM generated for the current account.

## Resume Last

**Resume Last** uses the selected workspace’s saved launch history:

- last account
- last profile
- last launch type

If the last launch type was CLI, it relaunches CLI Companion.
Otherwise it relaunches Desktop.

## Kill Session

Each active session row has a **Kill** button.

Kill behavior is now targeted:

- tracked Desktop sessions are terminated by their own process ID
- best-effort Desktop sessions must resolve a unique desktop target before they are killed
- stale `desktop_store` placeholders with no live Desktop instance may be retired instead of
  blocking launch
- CLI companion sessions continue to use their own marker-based kill path

If the desktop target cannot be proven, the row stays visible and the app shows a warning instead
of silently removing the session.

## Snapshot Session

Each active session row has a **Snapshot** button.

This exports a Markdown snapshot of the account’s local data:

- `orbit.db` if present
- `history.jsonl` if present

The snapshot is written under the switcher snapshots folder and is meant for local diagnostics.
It is **not** a raw database copy.

## Settings reference

The Settings window exposes:

- **Minimize to tray**
- **Git Guard**
- **Prefer Windows Terminal for CLI**
- **Windows sandbox mode**
- **Auto-trust workspace on launch**
- **Refresh all account configs**

### What the settings do

| Setting | Effect |
|---|---|
| Minimize to tray | Keeps the app in the tray when the main window is closed |
| Git Guard | Warns before launching when the selected workspace repo is dirty |
| Prefer Windows Terminal for CLI | Uses Windows Terminal tabs for CLI Companion launches when available |
| Windows sandbox mode | Writes the requested sandbox mode into generated account config |
| Auto-trust workspace on launch | Marks the selected workspace as trusted on launch |
| Refresh all account configs | Regenerates per-account runtime config with the currently selected sandbox mode |

## Tray behavior

If **Minimize to tray** is enabled, closing the main window hides the app instead of exiting.

Use the tray menu or explicit exit action to shut the app down fully.

## Diagnostics artifacts

Useful local artifacts include:

- `%USERPROFILE%\.codex-switcher\last_launch_plan.json`
- `%USERPROFILE%\.codex-switcher\last_desktop_launch_plan.json`
- `%USERPROFILE%\.codex-switcher\last_desktop_deeplinks.txt`
- `%USERPROFILE%\.codex-switcher\sessions\...`
- `%USERPROFILE%\.codex-switcher\snapshots\...`
- `%USERPROFILE%\.codex-switcher\logs\...`

These files are intended for local troubleshooting and launch inspection.

## Security

- API keys are protected at rest with Windows DPAPI
- OAuth tokens remain in Codex’s native account storage
- Snapshot exports may contain sensitive conversation content, so keep them local

## Troubleshooting

| Issue | Suggested fix |
|---|---|
| Desktop executable not found | Set the Desktop path manually, or use the CLI fallback if available |
| `wt.exe` not found | Install Windows Terminal from the Microsoft Store |
| Workspace launch looks wrong | Check `last_launch_plan.json` and the Desktop deeplink artifacts |
| Git Guard blocks a launch | Commit or stash your changes, or review the warning and retry |

## Known limitations

- **Desktop is effectively a single global app instance.**
  CEM can help switch the managed account behind it, but it is still one global Desktop app.
- **Store-app workspace binding remains best-effort/diagnostic.**
  The app records launch attempts and deep-link behavior, but binding may not be fully provable.

## License

MIT — use at your own risk. This tool manages your own accounts only.
