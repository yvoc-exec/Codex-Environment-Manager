- bugs
- workflow gaps
- logic defects
- architecture risks
- security/data-loss risks
- testing/maintainability gaps

## Review constraints
- Static review only; no successful build/run validation was available in this session.
- Findings below are based on repository inspection and code-path tracing.

## Executive summary
The project has a solid concept and several thoughtful safety features, but there are still
important correctness and workflow risks in launch/session handling. The highest-risk issue is
shell/batch injection/breakage in generated CLI launcher scripts caused by insufficient escaping of
user-controlled values. The next tier of issues centers on API-key launch reliability, non-
transactional desktop launch behavior, incorrect Store-app desktop session tracking, and onboarding/
session-management gaps. Documentation and architecture are also drifting from the implemented
runtime behavior.

---

## P0 Findings

### P0-1: Generated CLI launcher script is vulnerable to command breakage/injection from user-
controlled values
**Impact**
- CLI launches can fail or behave unpredictably for valid user-entered names/values.
- User-controlled account/profile/workspace display names or env-var values can break `launch.cmd`.
- In the worst case, embedded shell metacharacters can execute unintended commands.

**Evidence**
- `Services/CodexProcessManager.cs:374-375`
  `EscapeBatchValue()` only escapes `^` and `%`.
- `Services/LauncherService.cs:367-377`
  The generated batch script interpolates `acct.Name`, `persona.Name`, `ws.Name`, and
  `persona.EnvVars` directly into lines like:
  - `set "CEM_EXPECTED_ACCOUNT_NAME=..."`
  - `set "CEM_EXPECTED_PERSONA_NAME=..."`
  - `set "CEM_EXPECTED_WORKSPACE_NAME=..."`
  - `set "{kv.Key}=..."`
- Those values are user-controlled and are not safely escaped for embedded `"` / `&` / `|` / `<` /
  `>` cases.

**Recommendation**
- Stop building critical launcher state via raw batch string interpolation where possible.
- Prefer structured process launching (`ProcessStartInfo.ArgumentList`, direct environment
  injection, or a PowerShell wrapper that receives structured values safely).
- If batch must remain, implement correct cmd-safe escaping for quoted `set "VAR=value"` usage.
- Add validation/tests for names and env values containing quotes and shell metacharacters.

---

## P1 Findings

### P1-1: API-key fallback injection is not actually used for launch paths
**Impact**
- API-key accounts can fail to launch if bootstrap login did not complete successfully.
- Runtime behavior does not match documented behavior.

**Evidence**
- `README.md:68-72` says CLI launch sets `OPENAI_API_KEY`.
- `Services/LauncherService.cs:130` calls `ApplyEnvironment(... includeApiKeyFallback: false)` for
  desktop.
- `Services/LauncherService.cs:153-186` launches CLI through generated script and never calls an
  API-key fallback path.
- `Services/LauncherService.cs:319-330` contains fallback env injection logic for `OPENAI_API_KEY` /
  `CODEX_API_KEY`, but it is effectively unused for normal launches.

**Recommendation**
- Either:
  1. inject API-key fallback env vars when account type is `api_key` and bootstrap/auth state is
  missing, or
  2. block launch with a clear auth-state error instead of silently relying on best-effort
  bootstrap.
- Reflect actual behavior in UI and docs.

### P1-2: Desktop launch is not transactional; failures can kill the current desktop and still leave
switched state behind
**Impact**
- A failed desktop launch can close the user’s current desktop session before replacement is
  confirmed.
- `.codex` may already be swapped to a different account when launch fails.
**Evidence**
- `Services/LauncherService.cs:90-105`
  - validates workspace
  - optionally runs Git Guard
  - kills desktop
  - kills tracked desktop sessions
  - mutates account/profile/runtime config
  - swaps the junction
- `Services/LauncherService.cs:122-133`
  - only after that does it resolve fallback/start and call `Process.Start(...)`.

**Recommendation**
- Pre-validate all fallible launch prerequisites before killing the existing desktop.
- Make launch two-phase/transactional:
  - prepare config
  - validate executable/fallback path
  - then swap/kill/start
- Add rollback for failed post-swap launch attempts.

### P1-3: Store-app (`codex app`) desktop sessions are tracked by the wrong PID
**Impact**
- Active Sessions can show the desktop session as exited even while Codex Desktop remains open.
- Kill/snapshot/session visibility become unreliable for Store-app launches.

**Evidence**
- `Services/LauncherService.cs:122-147`
  - when desktop exe is missing, the app uses `codex app`
  - still registers the returned process with `ProcessId = proc.Id`
- `Services/SessionManager.cs:196-203`
  - pruning uses `ExitMarkerPath` or `ProcessId.HasExited`
- If `codex app` exits quickly after launching the Store app, the tracked PID is only the helper,
  not the real desktop app.

**Recommendation**
- For `codex app` launches, track the real desktop window/process instead of the helper PID.
- If that is not possible, use a different session model for Store-app desktop launches and avoid
  pruning solely on helper-process exit.

### P1-4: First-run wizard can loop forever when settings are empty
**Impact**
- Users can complete/skip onboarding and still be forced back into the wizard on next app start.

**Evidence**
- `MainWindow.xaml.cs:75-83`
  - first-run wizard is triggered if `accounts.Count == 0 || settings.Count == 0`
- `Views/FirstRunWizard.xaml.cs:186-194`
  - settings are only written if `_detectedDesktopPath` is non-empty
- So a valid “finish without desktop path” flow can still leave `settings.json` empty forever.

**Recommendation**
- Always persist a default `AppSettings` record when the wizard completes.
- Better: use a dedicated onboarding-complete flag instead of inferring onboarding from
  `settings.Count`.

### P1-5: Killing a session removes it from tracking even when the kill attempt failed
**Impact**
- A still-running CLI session can disappear from Active Sessions, leaving the user with no control
  surface.
- The app can report an effective removal without proving the process exited.

**Evidence**
- `Services/SessionManager.cs:70-72`
  - `TryWriteExitMarker(...)`
  - `_active.Remove(s);`
  - `Save();`
- This happens after best-effort kill attempts, regardless of whether the target process actually
  exited.

**Recommendation**
- Only remove the session after confirmed process exit or successful authoritative kill.
- If kill fails, keep the session row and surface an error/warning.

### P1-6: Clearing the Desktop path in Settings does not clear the in-memory override until restart
**Impact**
- Users can save an empty desktop path expecting fallback behavior, but the app may continue using
  the stale override path in memory.

**Evidence**
- `Views/SettingsWindow.xaml.cs:64-76`
  - allows saving an empty `CodexDesktopPath`
- `MainWindow.xaml.cs:843-851`
  - only updates `_processManager.OverridePath` if the saved path is non-empty

**Recommendation**
- On settings save, explicitly clear `_processManager.OverridePath` when the saved path is blank.

---

## P2 Findings

### P2-1: Documentation is materially out of sync with current code
**Impact**
- Operators and future maintainers will follow the wrong workflow and debug the wrong behaviors.

**Evidence**
- `README.md:16-17`
  - still advertises `Usage Tracking` and `Burp Bridge`
- `README.md:63-66`
  - still says launch injects managed blocks into `AGENTS.md`
- Current code/comments and implementation summary indicate Usage/Burp were descoped and active role
  selection now lives in profile-scoped instruction files plus compact account-level `AGENTS.md`.

**Recommendation**
- Update README and any operator docs to match the current architecture exactly.

### P2-2: No automated tests or CI were found
**Impact**
- High regression risk, especially around launcher/session/config rewriting logic.
- The most failure-prone code paths are also the least protected.

**Evidence**
- Repository scan found no test projects, no obvious test files, and no CI workflow files.

**Recommendation**
- Add at minimum:
  - unit tests for `ConfigService`
  - unit tests for `PersonaEngine` TOML rewrite logic
  - unit tests for `LauncherService` arg/env/script generation
  - session-pruning/kill tests for `SessionManager`
  - CI build + test workflow

### P2-3: `PersonaEngine` is too large and still carries dead legacy responsibilities
**Impact**
- Hard to reason about.
- Hard to test.
- Increases regression likelihood during future changes.

**Evidence**
- `Services/PersonaEngine.cs` is a large multi-responsibility file that mixes:
  - config mutation
  - TOML rewrite/validation
  - role-catalog generation
  - profile instruction generation
  - workspace-instruction generation
  - legacy managed-section helpers
- Legacy/dead-looking helpers still present:
  - `UpsertManagedSection`
  - `AppendManagedSection`
  - `UpsertPersonaBlockInAgents`
  - Burp marker constants

**Recommendation**
- Split into focused services/modules:
  - profile config writer
  - instruction file generator
  - workspace instruction generator
  - TOML utility/parser layer
- Remove dead/descoped code paths once replacement architecture is confirmed.

### P2-4: Refreshing role catalogs can preserve an invalid root profile reference
**Impact**
- `config.toml` can end up pointing to a profile name that no longer exists in the generated profile
  set.

**Evidence**
- `Services/PersonaEngine.cs:223-239`
  - reads current root `profile`
  - if non-empty, writes it back via `MergeManagedTomlProfiles(...)`
  - does not verify that `activeProfile` still exists in the current `personas` list

**Recommendation**
- If the stored root profile is missing from the regenerated profile map, fall back to a valid
  generated profile and log/warn clearly.

### P2-5: Snapshot export writes plaintext conversation data with no redaction or protection
**Impact**
- Sensitive prompts/responses/history can be exported into plain markdown under the switcher
  directory.
- This is a security/data-handling risk even if intentional.

**Evidence**
- `Services/SnapshotExporter.cs:34`
  - writes snapshot markdown directly to disk
- `Services/SnapshotExporter.cs:45` / `138`
  - exports `orbit.db` and `history.jsonl` content

**Recommendation**
- Add a clear warning before export.
- Consider optional redaction and/or protected export location naming.
- Document that snapshots may contain sensitive conversation content.

---

## Cross-cutting architecture observations

1. **Process/session/config logic is tightly coupled**
   - `MainWindow`, `LauncherService`, `SessionManager`, and `PersonaEngine` collectively own a lot
     of workflow logic with limited seam points for testing.

2. **Shell-script generation is carrying too much correctness risk**
   - The most brittle code path in the app is also one of the most critical.

3. **State mutation happens before proof of success**
   - Especially in desktop launch flow.

4. **Docs and runtime behavior are drifting**
   - README/operator expectations no longer match the real launch/config model.

---

## Suggested remediation order

1. Fix batch/script escaping or replace the risky generation path.
2. Fix API-key launch reliability.
3. Make desktop launch transactional / failure-safe.
4. Fix Store-app desktop session tracking.
5. Fix session-kill truthfulness and first-run/settings workflow bugs.
6. Update README/docs.
7. Add automated tests and CI.
8. Refactor large services and remove dead legacy code.

---

## Minimum validation plan after fixes

- Test profile/account/workspace names containing:
  - spaces
  - quotes
  - `&`
  - `|`
- Test API-key accounts with:
  - successful bootstrap
  - failed/missing bootstrap
- Test desktop launch for:
  - normal exe path
  - `codex app` fallback
  - failure before `Process.Start`
  - failure after config generation but before final launch
- Test session kill for:
  - success
  - failure
  - already-exited process
- Test first-run onboarding with:
  - no desktop exe
  - CLI installed only
  - import skipped
- Test config refresh after deleting/renaming profiles.
- Add regression tests for TOML generation and profile existence validation.

## Final note
This review is static-only. Before closing any follow-up fix work, run a real Windows validation
pass against Codex Desktop, `codex app`, Windows Terminal CLI mode, and API-key account flows.
