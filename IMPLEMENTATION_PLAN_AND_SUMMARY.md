# Codex Environment Manager — Patch Pass 5 Implementation & Review Summary

## Scope

This pass fixes the remaining high-impact issues found in pass 4 while keeping the intentionally hardcoded default profile model choices for token/cost control. The defaults remain user-editable through the profile editor.

## Implemented fixes

### Config/TOML correctness

- Replaced fragile append-only `config.toml` managed block behavior.
- `cli_auth_credentials_store = "file"` is now force-upserted as a root key for all managed accounts.
- `profile = "<active-profile>"` is now root-upserted safely without duplicating existing root keys.
- All CEM profile profiles are retained under `[profiles.cem_*]` instead of only keeping the last launched profile.
- Legacy CEM TOML blocks are removed before writing new profile blocks.
- Old standalone `[profiles.cem_*]` sections are removed before regenerated profiles are appended.
- Basic validation now rejects duplicate root keys and duplicate managed profile sections after writing.

### Authentication/account isolation

- Imported `.codex` accounts are normalized immediately to use file-based credential storage.
- API-key login bootstrap now runs in the background to avoid freezing the WPF UI.
- API-key login bootstrap uses timeout-bound async process waiting and kills the process tree on timeout.
- API key fallback environment injection remains available only as a fallback path.

### Workspace/context safety

- Profile launch instructions remain generated outside the repo by default under `~/.codex-switcher/generated`.
- Burp context import now saves generated context outside the repo by default.
- Burp context only writes to repo `AGENTS.md` when the explicit Settings option is enabled.
- Burp managed markers now include a stable section id so future managed sections do not collide.

### Runtime/session handling

- CLI launch scripts now write session `started.txt` and `exit.txt` sentinels.
- Session records include `ExitMarkerPath`.
- Session pruning now treats `exit.txt` as authoritative when present.
- Killing sessions still uses `Kill(entireProcessTree: true)`.
- Tray Exit now calls `MainWindow.RequestExit()` so it does not fight minimize-to-tray behavior.

### Git Guard

- Git Guard no longer writes backups inside the target repository.
- Patch/meta backups now go under `~/.codex-switcher/git-backups/<repo-hashish-name>/`.
- Auto-commit remains removed; Git Guard only offers patch backup, continue, or cancel.

### Profile management

- Added `ProfileEditorWindow`.
- Users can now edit:
  - profile name
  - icon
  - model
  - reasoning effort
  - sandbox mode
  - approval policy
  - template path
  - extra CLI args
  - environment variables
- Default Planner/Reviewer/Implementor model choices remain hardcoded as requested.

## Intentional non-change

The default model strategy remains hardcoded:

- Planner: `gpt-5.4` with high reasoning
- Reviewer: `gpt-5.5` with medium reasoning
- Implementor: `gpt-5.4-mini` with high reasoning

This is intentional to support token/cost control. The new profile editor allows manual adjustment when needed.

## Remaining validation requirement

This sandbox does not have the .NET SDK installed, so `dotnet build` could not be executed here. Validate on Windows with:

```powershell
dotnet restore
dotnet build
```

Then test with a disposable Codex account folder before using real `~/.codex` data.

## Review result after pass 5

Static review score:

```text
Architecture:        9/10
Security direction:  9/10
Workflow UX:         9/10
Runtime safety:      8.5/10
Build confidence:    8/10 static-only, pending Windows/.NET build
```

True 10/10 requires live validation on Windows with installed Codex CLI/Desktop, because Codex auth/profile/Desktop behavior cannot be proven inside this Linux sandbox.


## Pass 6 update

Adjusted default profile reasoning strategy as requested:

- Planner = `gpt-5.4` + high reasoning
- Reviewer = `gpt-5.5` + medium reasoning
- Implementor = `gpt-5.4-mini` + high reasoning

No changes were made to the hardcoded default model IDs beyond the reviewer reasoning-effort correction.


## Pass 7 Review Loop

Additional fixes after re-review:

- Fixed a compile-breaking quote typo in `LauncherService.EscapeBatchTitle()`.
- Added migration so existing built-in Reviewer profiles using `gpt-5.5` with old `high` reasoning are corrected to `medium` reasoning.
- Improved Git Guard backups to include a ZIP archive of untracked file contents when untracked files are present, while still writing binary diffs for tracked/staged changes.
- Marked enriched session display fields with `JsonIgnore` so `sessions.json` persists only durable session data.

Current default model strategy remains:

```text
Planner     = gpt-5.4       high reasoning
Reviewer    = gpt-5.5       medium reasoning
Implementor = gpt-5.4-mini  high reasoning
```

Static review status after pass 7:

```text
Architecture:        9.4/10
Security direction:  9.4/10
Workflow UX:         9.3/10
Runtime safety:      9.0/10
Build confidence:    8.8/10 static-only
```

A true 10/10 still requires Windows-side `dotnet restore`, `dotnet build`, and live Codex CLI/Desktop validation because this sandbox cannot execute .NET/WPF or Codex Desktop.

## Pass 8 final pre-Windows loop

- Hardened Git Guard command execution so stdout/stderr are read concurrently and git commands time out instead of risking UI hangs/deadlocks.
- Strengthened generated `config.toml` validation by detecting duplicate keys inside managed Codex profile profiles, not just duplicate root keys.
- Made session display honor CLI exit marker files immediately.
- Persisted CLI started marker paths for better diagnostics.
- Added a defensive `cd` failure check inside generated CLI launcher scripts.
- Re-ran static checks: zip integrity, XAML XML parse, XAML event handler matching, and basic C# string/brace sanity.

## Pass 9 - Windows build feedback fix

- Fixed ambiguous WPF/WinForms type resolution caused by enabling WinForms for tray icon support.
- `App.xaml.cs` now aliases WPF `Application`/`MessageBox` explicitly.
- `ProfileEditorWindow.xaml.cs` now aliases WPF `ComboBox`/`ComboBoxItem` explicitly.

Validation target from user machine:

```powershell
dotnet build -c Debug
```


## Pass 10 - WPF/WinForms ambiguity fix

- Fixed remaining CS0104 ambiguous references caused by `UseWindowsForms=true` plus WPF UI code.
- Added explicit aliases for WPF MessageBox and Microsoft.Win32 OpenFileDialog where needed.
- Qualified WPF Button checks in MainWindow.
- Initialized SettingsWindow `_settings` to avoid nullable warning.


## Pass 11

- Fixed Microsoft.Win32.OpenFileDialog usage in BurpBridge_Click: removed unsupported Owner property and passed the owner window via ShowDialog(this).


## Pass 15 update

- Added Codex Desktop fallback through `codex app` when the Microsoft Store app has no normal browsable executable.
- First-run wizard now reports Store/CLI fallback instead of requiring manual Desktop path browse.
- Settings now clarifies the Desktop path is optional when `codex app` fallback is available.
- Desktop launch still applies account/profile config first, then starts Codex app from the selected workspace directory.


## Pass 16 — Codex Desktop detection refinement

- Added explicit Codex Desktop detection modes: Win32/manual installer executable, Microsoft Store AppX package, and Codex CLI `codex app` fallback.
- First-run wizard now reports whether Desktop was found via manual/installer path, Microsoft Store package, or CLI fallback.
- Manual EXE detection checks common LocalAppData/Program Files locations and registry App Paths.
- Store detection checks the `OpenAI.Codex` AppX package and still uses `codex app` for managed launching when the app is Store-installed.

## Pass 17 — UI dark theme polish

- Added a global WPF dark theme in `App.xaml` for buttons, list boxes, list items, combo boxes/dropdowns, combo box items, text boxes, password boxes, checkboxes, scrollbars, and disabled controls.
- Replaced the main window's default white OS title bar with a dark custom title/header bar.
- Added custom minimize, maximize/restore, and close buttons for the main window.
- Styled vertical and horizontal scrollbars to match the dark UI.
- Removed local implicit control styles that overrode global dark templates.
- Darkened the input dialog background so prompt/input dialogs match the rest of the app.


## Pass 22 Desktop workspace opening fix

On Windows Store/AppX Codex Desktop, `codex app <path>` may launch the Desktop app but not automatically import/open the workspace. Pass 22 now passes the workspace path to `codex app` and schedules a Windows UI bridge that focuses Codex Desktop, invokes Open Folder, pastes the selected workspace path, and confirms the dialog. This keeps Desktop project selection aligned with the selected Account + Profile + Workspace workflow.


## Pass 23 - Desktop workspace bridge hardening

- Added UI Automation based Codex Desktop project opening for Windows Store/AppX Codex.
- Detects and clicks the first-run/new-account Agent sandbox setup gate when present.
- Invokes visible project selector controls such as `Work in a project` instead of relying only on Ctrl+O.
- Handles standard Windows folder dialogs by pasting the selected workspace path.
- Keeps SendKeys as last-resort fallback only.

## Pass 26 — Profile-first verified launch contract

- Codex profiles are now treated as the source of truth for model, reasoning effort, sandbox mode, and approval policy.
- CLI launch now uses an explicit contract: `codex --cd "<workspace>" --profile "<cem_profile>"`.
- Legacy profile CLI args that control model/profile/sandbox/approval are stripped during migration and launch so they cannot silently override profiles.
- `AGENTS.override.md` now contains behavioral rules only. It no longer tells Codex to claim a profile by name as a fake identity check.
- Each launch writes `CEM_ACTIVE_CONTEXT.json`, `last_launch_plan.json`, and per-session `launch_plan.json`.
- Main window now shows a launch preview/confirmation with account, CODEX_HOME, profile, model/reasoning/sandbox/approval, workspace, and the command contract.


## Pass 31 update

Rebuilt from a syntax-clean pass 26 base to avoid the raw multiline string corruption in pass 29/pass 30.

Current canonical CEM profiles:
- Planner+Reviewer GPT 5.4 Medium
- Planner+Reviewer GPT 5.4 High
- Planner+Reviewer GPT 5.5 High
- Reviewer GPT 5.5 Extra High
- Implementor GPT 5.4 Mini High

Architecture:
- Codex profiles control model/reasoning/sandbox/approval.
- Codex-level `AGENTS.md` contains the role catalog for all CEM profiles.
- Profile-scoped `developer_instructions` selects the active role from the catalog.
- Workspace `AGENTS.md` is not used for active profile switching.
- Profile panel now owns role template management and role catalog refresh.


## Pass 32 update

- Added per-account Windows sandbox config management:
  - `[windows] sandbox = "elevated"` by default
  - Settings can switch to `unelevated`
- Added workspace trust management:
  - on launch, selected workspace is written to selected account config as `[projects."<absolute path>"] trust_level = "trusted"`
  - can be disabled in Settings
- Launch Preview now shows sandbox/trust actions.
- This prevents repeated Codex prompts for sandbox setup and project trust per managed account/workspace.


## Pass 33 update

Completed the sandbox/trust checklist:

- Adds `[windows] sandbox = "elevated"` to managed account `config.toml` files when missing.
- Preserves existing per-account `sandbox = "elevated"` or `sandbox = "unelevated"` choices.
- Keeps the Settings selector for Windows sandbox mode.
- Adds Settings action: `Refresh all account configs`.
- Launch still writes selected workspace trust as `[projects."<absolute path>"] trust_level = "trusted"` when enabled.


## Pass 34 update

Fixed profile-not-found launch bug.

Root cause:
`EnsureAccountRuntimeConfig()` used `UpsertRootKeys()`, which removes CEM's managed TOML block before adding root keys. Because runtime config was applied after profile generation, it deleted all `[profiles.cem_*]` sections immediately before Codex launch.

Fix:
- Runtime/base account config now uses `UpsertRootKeysPreserveManaged()`.
- `[windows]` and `[projects]` config updates preserve the CEM managed profile block.
- Launch now validates the selected `[profiles.<name>]` exists in the selected account `config.toml` before spawning Codex.


## Pass 35 update

Strengthened active role selection.

Observed problem:
Codex could load the role catalog and model/reasoning profile, but the active CEM profile name was not explicit enough inside the session. The model inferred a Planner/Reviewer-style role without seeing the exact active CEM profile ID/name.

Fix:
- Each Codex profile now gets a profile-scoped `model_instructions_file`.
- CEM generates `<CODEX_HOME>/cem-profiles/<profile>.instructions.md` for every CEM profile.
- That file explicitly states the active CEM profile name, Codex profile name, model, reasoning, sandbox, approval, and exact behavior rules.
- Existing `developer_instructions` remains as a short active-role selector, but `model_instructions_file` is now the stronger source of truth.
- Launch validation now also checks the active instruction file exists before spawning Codex.


## Pass 36 update

- Renamed the editor window title and header from Profile to Profile.
- Reworked the Profile editor so controlled settings use dropdown menus instead of freeform text where practical.
- Model, reasoning effort, sandbox mode, approval policy, icon, and role template are now dropdown selections.
- Extra CLI args now use a curated preset dropdown with add/remove controls to keep values valid and predictable.
- Env vars now use a curated preset dropdown with add/remove controls to keep values valid and predictable.
- Added hover tooltips / short descriptions across controls to help users understand each setting.
- Updated several user-facing Profile labels/messages in the main UI to Profile wording.


## Pass 37 update

- Renamed remaining user-facing Persona wording to Profile where safe.
- Kept internal class names such as Persona and PersonaEngine unchanged to avoid unnecessary refactor risk.
- Updated launch proof wording from Expected persona to Expected profile.
- Updated duplicate/delete/select message boxes to use Profile wording.
- Updated documentation/summary wording to align with the profile-first architecture.


## Pass 38 update

- Fixed pass 37 build regression caused by user-facing wording cleanup touching internal type names.
- Restored internal `Persona` and `PersonaEngine` references in `LauncherService.cs` while keeping UI wording as Profile.
- Fixed WPF/WinForms `ComboBox` ambiguity in `PersonaEditorWindow.xaml.cs` using WPF aliases.


## Pass 39 update

- Fixed pass 38 build regression: `Session` still uses internal `PersonaId`, not `ProfileId`.
- Fixed `PersonaEngine.RefreshRoleCatalogForAccount()` variable typo from `account` to `acct`.
- Kept user-facing Profile wording while preserving internal Persona model names.


## Pass 40 update

- Fixed Profile editor dropdowns showing internal `Choice` type names.
- Added `Choice.ToString()` fallback returning the friendly label.
- Added explicit ComboBox item template binding to `Choice.Label` with `Choice.Description` as tooltip.


## Pass 41 update

- Added a Refresh button beside Role template in the Profile editor.
- Role template dropdown now reloads from the templates/personas folder on demand.
- Removed the ComboBox item template override that caused selected/collapsed values to display internal Choice objects in some app styles.
- Added TextSearch label binding and cached template selection handling.


## Pass 42 update

- Moved the three template/AGENTS actions from the left Profile panel into the Profile Settings window.
- Added Template tools row immediately before Role template selection.
- Profile Settings now has buttons for Templates folder, Linked template, and AGENTS refresh request.
- Role template list refresh now checks the writable ~/.codex-switcher/templates/personas folder and the app-bundled Templates/personas folder.


## Pass 43 update

- Fixed Role template dropdown source mismatch.
- Dropdown now lists only the same user folder opened by the Templates button: `~/.codex-switcher/templates/personas`.
- Removed app-bundled fallback templates from the visible dropdown so `planner.md`, `reviewer.md`, and `implementor.md` do not appear unless they actually exist in the user folder.
- Refresh now mirrors actual user folder content exactly for `*.md` role template files.


## Pass 44 update

- Improved Active Sessions kill behavior for Windows Terminal CLI tabs.
- Session model now stores `LauncherScriptPath` and `StopMarkerPath`.
- CLI launch records the session launcher script path.
- Kill now tries normal tracked PID first, then finds `cmd.exe /k <session>\\launch.cmd` by command line and kills that process tree.
- This is designed to close/kill Codex CLI sessions launched in Windows Terminal tabs, where `wt.exe` PID tracking is unreliable.


## Pass 45 update

- Added read-only generated file viewer.
- Launch panel now includes `👁 View Files`.
- Viewer opens selected account/profile generated `config.toml`, `AGENTS.md`, and `cem-profiles/<profile>.instructions.md` in a read-only text window.
- Viewer supports Refresh, Copy Path, and Copy Content.


## Pass 46 update

- Removed full role-catalog duplication from Codex-level `AGENTS.md`.
- `AGENTS.md` is now compact shared CEM account guidance only.
- User-editable role templates remain the only behavior source: `~/.codex-switcher/templates/personas/*.md`.
- Profile-specific instruction files are generated to `<CODEX_HOME>/cem-profiles/*.instructions.md`.
- `config.toml` profile entries point to the generated `model_instructions_file`.
- Renamed refresh wording from `AGENTS` to `Instructions`.
- Read-only viewer descriptions updated to reflect compact AGENTS.md and generated profile instructions.


## Pass 47 update

- Fixed build error in GeneratedFilesViewerWindow caused by ambiguous `Clipboard` reference.
- Uses explicit `System.Windows.Clipboard.SetText(...)` because the project enables both WPF and Windows Forms.


## Pass 48 update

- Hardened account deletion against locked files such as Git pack `pack-*.idx` files.
- Account folder deletion now uses quarantine-first flow: move account folder to `deleted_accounts/<id>_<timestamp>` before removing metadata.
- Clears read-only attributes and retries recursive delete.
- If Windows still locks a file, the account is removed from CEM, the folder remains quarantined, and the UI shows a cleanup-needed warning instead of crashing.
- Delete account handler now catches IOException/UnauthorizedAccessException and keeps the app alive.


## Pass 49 update

- Fixed Windows Terminal CLI tab grouping by launching with a stable named WT window: `wt -w CodexEnvironmentManager new-tab ...`.
- Added unique per-session kill marker `CEM_SESSION_<sessionId>` to each CLI session command line and environment.
- Kill now targets only cmd.exe/powershell/pwsh processes containing the selected session marker or exact selected launch script path.
- Kill no longer matches/kills WindowsTerminal.exe/wt.exe, preventing one kill from closing all tabs/windows.


## Pass 50 update

- Improved Windows Terminal CLI kill behavior after pass 49.
- `launch.cmd` now detects the per-session stop marker after Codex exits and exits cleanly with code 0.
- Kill now targets child processes of the selected session's cmd/powershell root first instead of killing the root tab shell immediately.
- This lets the selected tab/session close cleanly while keeping other Codex CLI tabs alive.


## Pass 51 update

- Fixed Windows Terminal killed-tab lingering on the `[process exited]` screen.
- Windows Terminal CLI tab launches now use `cmd.exe /c` instead of `cmd.exe /k`.
- Standalone non-Windows-Terminal fallback still uses `/k` so the command window remains visible.
- Keeps pass 49 shared WT window/tab grouping and pass 50 selected-session kill behavior.


## Pass 53 update

- Rolled back pass 52's fragile PowerShell PID wrapper because it caused CLI tabs to open then crash.
- Kept pass 49/51 Windows Terminal shared named-window tab grouping and `/c` close-on-exit behavior.
- Kept unique per-session marker based kill, but made fallback safer by avoiding broad Windows Terminal/root process kills.
- Added explicit `exit /b %CODEX_EXIT_CODE%` at the end of launch.cmd for predictable tab termination behavior.


## Pass 54 update

- Fixed CLI tab sessions not reliably appearing in Active Sessions after Windows Terminal launch.
- CLI sessions are now registered after `Process.Start` returns even when Windows Terminal does not provide a useful long-lived PID.
- Active Sessions refresh now prunes stale sessions first and labels marker-tracked CLI sessions as `(cli)` instead of `(desktop)`.
- CLI launch triggers an immediate and delayed UI refresh to avoid stale Active Sessions rows after WT handoff.


## Pass 55 update

- Fixed CEM removing the selected CLI session row while the actual selected tab/process kept running.
- CLI launch now uses a safer per-session PowerShell wrapper that launches `cmd.exe /d /s /c call <codex.cmd> ...` instead of launching `codex.cmd` directly.
- The wrapper records the child cmd PID to `codex-wrapper.pid`.
- Kill first terminates the recorded child wrapper PID/tree for the selected session only, then falls back to marker/script matching if needed.
- Kept pass 54 active-session tracking fixes and pass 49 Windows Terminal tab grouping.


## Pass 56 update

- Reworked Usage button into account-scoped Codex status probes.
- Usage no longer injects `/status` into active user CLI tabs/sessions.
- Usage starts isolated hidden temporary Codex CLI probes with each account's CODEX_HOME.
- It sends `/status` once per account, captures output, sends `/quit`, and kills only the temporary probe on timeout.
- Results include active session count, active profiles/workspaces, captured status output, and legacy local logged cost if present.


## Pass 57 update

- Fixed Usage status probe failing with `codex.cmd is not recognized`.
- Usage probes now launch `.cmd/.bat` Codex CLI through `cmd.exe /d /s /k call "codex.cmd" ...` so the quoted npm shim resolves correctly and stdin remains available for `/status`.
- Added built-in hidden profile `cem_usage_status_probe` per account.
- The hidden usage profile uses read-only sandbox, never approval, low reasoning, and a tiny generated instruction file.
- Patched generic Codex CLI `.cmd/.bat` process creation to use `call` as well.


## Pass 58 update

- Improved Usage button status probing and output formatting.
- Usage probe now waits for Codex startup prompt before sending `/status`, then waits for Account/limit/status lines before `/quit`.
- Added parsing for Model, Directory, Permissions, Agents.md, Account, Collaboration mode, Session, 5h limit, and Weekly limit.
- Added `codex login status` background probe per account as an auth/API-key fallback.
- Usage report now shows structured account status first and preserves raw `/status` output for debugging.
\n\n## Pass 59 update\n\n- Fixed Usage hidden probe launcher quoting bug where cmd.exe received backslash-quoted `codex.cmd` and failed with `codex.cmd is not recognized`.\n- For `.cmd/.bat` Codex CLI shims, Usage now runs from the shim directory and calls `codex.cmd` by file name.\n- Usage status probe now uses `--cd <usage-probe-dir> --profile cem_usage_status_probe`.\n- Login-status probe uses the same shim-directory launch strategy.\n- Updated cmd quoting to use literal double-quotes instead of backslash-quoted strings.\n

## Pass 60 update

- Fixed Usage probe still dropping to plain `cmd.exe` where `/status` and `/quit` were interpreted as CMD commands.
- Usage now generates temporary batch launchers per account instead of embedding the full Codex command inline in `cmd.exe /k`.
- `cem_usage_probe.cmd` launches Codex with the hidden `cem_usage_status_probe` profile and keeps stdin connected for `/status`.
- `cem_login_status_probe.cmd` runs `codex login status` with the same account CODEX_HOME.
- This avoids Windows command-line quote parsing problems with npm `codex.cmd` shims.


## Pass 61 update

- Fixed pass 60 build failure: `ProbeSingleAccountAsync` was accidentally removed during method replacement.
- Restored `ProbeSingleAccountAsync` and `BuildStatusResult` while keeping pass 60's temporary batch launcher approach.
- Verified UsageStatusService contains status probing, login-status fallback, parser helpers, and batch launcher helpers.


## Pass 62 update

- Changed Usage behavior after confirming hidden `/status` probes fail because Codex CLI requires a real terminal (`stdin is not a terminal`).
- Usage no longer sends `/status` or `/quit` through redirected hidden stdin.
- Usage now uses the reliable non-interactive `codex login status` probe per account.
- Usage dialog shows account auth status, active CEM session counts, profiles/workspaces, and a clear note that 5h/weekly bars require interactive `/status` or the Codex usage URL.
- Removed noisy `/status is not recognized`, `/quit is not recognized`, and `stdin is not a terminal` output from the normal Usage path.


## Pass 63 update

- Descoped Usage and Burp Bridge from the project.
- Removed sidebar Usage and Burp Bridge buttons.
- Removed Usage click handler, local usage tracker wiring, and usage status probe wiring.
- Removed Burp Bridge click handler.
- Deleted descoped service files: `UsageTracker.cs`, `UsageStatusService.cs`, and `BurpBridge.cs`.
