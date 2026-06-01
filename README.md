# Codex Environment Manager

Codex Environment Manager (CEM) is a Windows app for managing multiple Codex and Kimi accounts,
personas, and workspaces on one machine.

It supports two provider families:

- **Codex**
  - multiple accounts
  - personas / profiles
  - Desktop launch
  - CLI Companion
  - selected workspace binding
  - Active Sessions tracking and kill
- **Kimi**
  - account-scoped OAuth and Moonshot API-key account flows
  - per-account Kimi home
  - CLI Companion only
  - selected workspace binding
  - generated per-session agent files

## What CEM manages

- **Accounts**
  - Codex Plus / OAuth accounts
  - Codex API-key accounts
  - Kimi OAuth accounts
  - Kimi Moonshot API-key accounts
- **Personas / profiles**
  - generated Codex profile instruction files
  - generated Kimi agent-file launch artifacts
  - per-profile launch options and validation
- **Workspaces**
  - local project folders
  - last-used account/profile/launch type for each workspace
- **Session diagnostics**
  - launch plans
  - active context files
  - active session rows
  - session snapshots
  - generated file viewer for account-side files

## Provider behavior at a glance

### Codex

- Uses the selected account's `CODEX_HOME`
- Uses managed profiles and generated `model_instructions_file` outputs
- Desktop launch uses `codex app --profile <profileName> <workspace>` where available
- Active Sessions records:
  - the requested profile name
  - the Codex profile actually requested from the launcher
  - profile override / verification status
  - the launch command preview
- Unmanaged Desktop instances are warned about and blocked so CEM does not silently launch with a
  profile mismatch

### Kimi

- Uses account-scoped login/setup and per-account Kimi home
- Uses `KIMI_CODE_HOME` and `KIMI_SHARE_DIR` for account isolation
- Kimi CLI Companion only; there is no Kimi Desktop path in CEM
- Launches the selected workspace with `--work-dir`
- Uses generated `--agent-file` behavior for persona/profile launch state
- Passes the selected model with `--model`
- Currently implemented optional Kimi controls:
  - thinking mode
  - plan mode
  - skills directories
  - MCP config file
  - additional workspace directories
- Kimi does **not** use Codex `config.toml` or Codex profile files

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
- detect Kimi CLI availability
- import an existing `.codex` folder into a managed account
- complete setup even if a Desktop executable is not available, as long as the fallback
  conditions are acceptable

Typical first-run flow:

1. Launch the app.
2. Review the Desktop / CLI detection status.
3. Optionally import an existing `.codex` folder.
4. Finish setup, then add any missing accounts, personas, or workspaces from the main window.

## Adding accounts

Use the account buttons in the main window:

- **+ Plus**: creates a Codex Plus / OAuth-backed account entry
- **+ API**: creates a Codex API-key account entry
- **Add Account**: opens the account wizard so you can pick Codex or Kimi and the matching auth type

Notes:

- API keys are stored using Windows DPAPI at rest
- OAuth-backed accounts keep their own isolated account home
- Kimi OAuth and Moonshot API-key accounts are isolated per account

## Adding workspaces

Add a workspace by selecting a local project folder and giving it a name.

Workspaces are used to:

- remember the last account/profile used with that folder
- restore the last launch type with **Resume Last**
- drive Desktop workspace binding and CLI `--cd` or `--work-dir` launches

## Launch contract confirmation

Before any launch, CEM shows a **Verify Launch Contract** prompt.

This preview is meant to confirm:

- the selected account
- the selected persona/profile
- the selected workspace
- the launch type you are about to run

You must confirm the prompt before the launch proceeds.

## Launching Codex Desktop

Desktop launch will:

1. preflight the selected account, profile, and workspace
2. write the active context and launch plan artifacts
3. verify the launch contract
4. close any existing tracked Desktop sessions only after the target can be verified
5. swap the `.codex` junction to the selected account
6. launch Codex Desktop

If a stale `desktop_store` placeholder is found during preflight and no live Desktop instance can
be proven, CEM may retire that placeholder instead of blocking the relaunch.

When the Codex app path is available, Desktop launch uses the profile override form:

```text
codex app --profile <profileName> <workspace>
```

Desktop diagnostics include:

- `last_launch_plan.json`
- `last_desktop_launch_plan.json`
- `last_desktop_deeplinks.txt`

### Desktop workspace binding

For the Store/AppX / `codex app` path, workspace binding is best-effort and diagnostic.
The app records the deep-link variants and activation attempts, but it does not claim that the
workspace binding is fully proven unless the code can verify it.

## Launching the Codex CLI Companion

CLI Companion launch:

- does **not** swap the global `.codex` junction
- scopes `CODEX_HOME` to the selected account
- launches Codex in a terminal session
- uses Windows Terminal if enabled in settings, otherwise falls back to `cmd.exe`

This is the most reliable way to run multiple parallel Codex sessions against different workspaces.

## Launching Kimi

Kimi launch is CLI-only and account-scoped:

- login / setup is opened per account
- the account's `KIMI_CODE_HOME` and `KIMI_SHARE_DIR` are set automatically
- the selected workspace is passed with `--work-dir`
- the selected persona/profile is represented by the generated `--agent-file`
- the selected model is passed with `--model`
- optional Kimi controls are emitted only when configured on the persona

CEM does not use Codex `config.toml` or Codex profile files for Kimi.

## Active Sessions

The Active Sessions view tracks launch rows and supports targeted kill behavior.

For Codex sessions, the stored session data includes:

- requested profile name
- profile launch method
- profile verification status
- command preview

For Kimi sessions, the active session row clearly shows the Kimi workspace context and the
session-level launch artifacts.

Kill behavior is targeted:

- tracked Desktop sessions are terminated by their own process ID
- best-effort Desktop sessions must resolve a unique desktop target before they are killed
- stale `desktop_store` placeholders with no live Desktop instance may be retired instead of
  blocking launch
- CLI companion sessions continue to use their own marker-based kill path

If the desktop target cannot be proven, the row stays visible and the app shows a warning instead
of silently removing the session.

## View Files

The **View Files** button opens the generated account-side files for the selected account/profile:

- `config.toml`
- `AGENTS.md`
- `cem-profiles/<profile>.instructions.md`

This is a quick way to inspect what CEM generated for the current account.

`AGENTS.md` remains compact shared guidance only. The role templates in
`~/.codex-switcher/templates/personas/*.md` are the source of truth for active role behavior.

## Settings reference

The Settings window exposes:

- **Minimize to tray**
- **Git Guard**
- **Prefer Windows Terminal for CLI**
- **Windows sandbox mode**
- **Auto-trust workspace on launch**
- **Refresh all account configs**
- **Kimi CLI path**

### What the settings do

| Setting | Effect |
|---|---|
| Minimize to tray | Keeps the app in the tray when the main window is closed |
| Git Guard | Warns before launching when the selected workspace repo is dirty |
| Prefer Windows Terminal for CLI | Uses Windows Terminal tabs for CLI Companion launches when available |
| Windows sandbox mode | Writes the requested sandbox mode into generated account config |
| Auto-trust workspace on launch | Marks the selected workspace as trusted on launch |
| Refresh all account configs | Regenerates per-account runtime config with the currently selected sandbox mode |
| Kimi CLI path | Points CEM at the Kimi CLI executable or shim |

## Diagnostics artifacts

Useful local artifacts include:

- `%USERPROFILE%\.codex-switcher\last_launch_plan.json`
- `%USERPROFILE%\.codex-switcher\last_desktop_launch_plan.json`
- `%USERPROFILE%\.codex-switcher\last_desktop_deeplinks.txt`
- `%USERPROFILE%\.codex-switcher\last_kimi_launch_plan.json`
- `%USERPROFILE%\.codex-switcher\sessions\...`
- `%USERPROFILE%\.codex-switcher\snapshots\...`
- `%USERPROFILE%\.codex-switcher\logs\...`

These files are intended for local troubleshooting and launch inspection.

## What is not included

- Usage tracking / Usage UI is descoped
- Burp Bridge is descoped
- Kimi Desktop launch is not supported

## Security

- API keys are protected at rest with Windows DPAPI
- OAuth tokens remain in the provider's native account storage
- Snapshot exports may contain sensitive conversation content, so keep them local

## Troubleshooting

| Issue | Suggested fix |
|---|---|
| Desktop executable not found | Set the Desktop path manually, or use the CLI fallback if available |
| `wt.exe` not found | Install Windows Terminal from the Microsoft Store |
| Kimi CLI not found | Set the Kimi CLI path in Settings or install a CLI shim on PATH |
| Workspace launch looks wrong | Check `last_launch_plan.json` and the Desktop deeplink artifacts |
| Git Guard blocks a launch | Commit or stash your changes, or review the warning and retry |

## Known limitations

- Desktop is effectively a single global app instance. CEM can help switch the managed account
  behind it, but it is still one global Desktop app.
- Store-app workspace binding remains best-effort and diagnostic.
- Kimi launch is CLI-only in this build.

## License

MIT - use at your own risk. This tool manages your own accounts only.
