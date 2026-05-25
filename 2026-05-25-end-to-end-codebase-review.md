# 2026-05-25 End-to-End Codebase Review

> Scope note: this revision re-triages the earlier static review against the pass 63 baseline.
> The goal is not to preserve every prior concern as a defect. The goal is to separate confirmed bugs,
> intentional design choices, already-addressed findings, maintainability work, and items that still need
> runtime validation.

## Pass 63 Runtime Reality Check

The following behaviors are confirmed as part of the current stable checkpoint unless otherwise noted:

- **Account/profile/persona selection works in Desktop.**
  - Status: Already Addressed
- **Codex Desktop correctly sees selected account/CODEX_HOME.**
  - Status: Already Addressed
- **Codex Desktop correctly sees selected CEM profile/persona/config.**
  - Status: Already Addressed
- **CLI Companion works with selected account/profile/workspace.**
  - Status: Already Addressed
- **Windows Terminal tab grouping works.**
  - Status: Already Addressed
- **Windows Terminal CLI lifecycle works.**
  - Status: Already Addressed
- **Kill selected CLI session works correctly.**
  - Status: Already Addressed
- **Safe account deletion works using quarantine-first deletion.**
  - Status: Already Addressed
- **Usage and Burp Bridge are intentionally descoped and removed.**
  - Status: Intentional / Accepted
- **AGENTS.md is intentionally compact shared guidance only.**
  - Status: Intentional / Accepted
- **Desktop workspace import/open is still a known limitation under investigation.**
  - Status: Needs Runtime Validation
- **CLI Companion is the workspace-guaranteed path through `codex --cd`.**
  - Status: Intentional / Accepted

## Current Source of Truth

The current architecture should be read as follows:

- **User-editable role behavior:** `~/.codex-switcher/templates/personas/*.md`
- **Generated active profile instructions:** `<CODEX_HOME>/cem-profiles/*.instructions.md`
- **Profile wiring:** `config.toml` points profiles to the generated instruction files
- **Runtime selected account/profile/workspace metadata:** `CEM_ACTIVE_CONTEXT.json`
- **Shared guidance only:** `AGENTS.md`

This is intentionally split so that `AGENTS.md` stays compact and does not duplicate the full role catalog.

## Intentional / Accepted Behavior

These are not defects. They are current architecture or deliberate product decisions.

| ID | Status | Finding | Notes |
|---|---|---|---|
| INT-1 | Intentional / Accepted | CEM creates generated files under each `CODEX_HOME`. | Expected part of local environment management. |
| INT-2 | Intentional / Accepted | CEM generates `config.toml` profile references. | This is the profile wiring mechanism. |
| INT-3 | Intentional / Accepted | CEM generates `cem-profiles/*.instructions.md` from user-editable templates. | Generated instructions are the active role source at runtime. |
| INT-4 | Intentional / Accepted | `AGENTS.md` is intentionally compact shared CEM guidance only. | Do not restore full role catalogs into `AGENTS.md`. |
| INT-5 | Intentional / Accepted | Usage Tracking is descoped and removed. | Do not reintroduce it as a recommendation. |
| INT-6 | Intentional / Accepted | Burp Bridge is descoped and removed. | Do not reintroduce it as a recommendation. |
| INT-7 | Intentional / Accepted | Snapshot export is intentionally plaintext markdown/history. | Keep local-only; add warning about secrets and sensitive context. |
| INT-8 | Intentional / Accepted | Windows Store app launch/session tracking may be best-effort unless a real Desktop process/window can be tracked. | Treat as a model limitation, not a hard failure, unless it lies about active state. |
| INT-9 | Intentional / Accepted | CLI Companion is the authoritative workspace-guaranteed launch mode. | `codex --cd` remains the reliable path. |

## Actual Bugs to Fix

These are confirmed defects or valid risks that should be treated as real fixes.

| ID | Status | Finding | Evidence / Why It Matters | Recommended Fix |
|---|---|---|---|---|
| P0-1 | Confirmed Bug | Generated CLI launcher script escaping / injection risk | Generated batch scripts still interpolate user-controlled account/profile/workspace/env-var values. Even if values are local-user supplied, they can break launches or enable command injection/breakage via quotes or shell metacharacters. | Harden batch escaping for `set`, `title`, `echo`, `cd`, and env lines. Add tests for names/values containing spaces, quotes, `&`, `|`, `<`, `>`, `%`, and `^`. Prefer structured process launch where practical, but do not require a full rewrite immediately. |
| P1-1 | Conditional Bug | API-key fallback injection not used | This is a bug only if API-key accounts remain in scope. | If API-key accounts remain supported, launch must either: (A) inject `OPENAI_API_KEY` / `CODEX_API_KEY` correctly when needed, or (B) block launch with a clear “API-key launch is not currently supported” message. If API-key accounts are descoped, update UI/docs and remove the `+ API` path. |
| P1-2 | Confirmed Bug | Desktop launch is not transactional | Desktop launch can kill the current Desktop or mutate account/profile state before the new launch is validated. That creates a workflow reliability risk. | Prevalidate launch prerequisites before killing existing Desktop. Resolve executable/fallback/deep-link strategy first. Then prepare config. Then switch account/profile. Then kill/start. Add rollback or a safe failure message if launch fails after state mutation. |
| P1-4 | Confirmed Bug | First-run wizard can loop forever when settings are empty | Onboarding currently appears to infer completion from settings state instead of persisting explicit completion. | Always persist default `AppSettings` when first-run completes. Add an onboarding-complete flag instead of inferring onboarding from settings count. Validate the flow where no Desktop path is detected but CLI exists. |
| P1-6 | Confirmed Bug | Clearing Desktop path does not clear the in-memory override | Clearing the saved Desktop path still leaves the live override active, so a restart should not be required for the fix to take effect. | If settings save a blank `CodexDesktopPath`, immediately clear `_processManager.OverridePath`. |
| P2-4 | Confirmed Bug | Invalid root profile reference can be preserved | A deleted or missing root profile can linger in generated config wiring, causing a broken root profile reference. | When regenerating `config.toml` profiles, if the root profile points to a deleted/missing profile, fall back to a valid generated profile or clear the root profile safely. Log a warning. |

## Already Addressed / Superseded by Later Passes

These items should not be treated as currently open defects unless a new regression proves otherwise.

| ID | Status | Finding | Notes |
|---|---|---|---|
| P1-5 | Mostly Addressed, Needs Regression Test | Killing a session removes it from tracking even when kill failed | This appears to have been mostly addressed by pass 55 for CLI selected-session kill. Keep a regression test: remove the session only after confirmed selected process/tree termination or after a known accepted removal mode. If kill fails, show a warning and keep/control the row when possible. Do not claim it is still confirmed broken without current evidence. |
| SUP-1 | Already Addressed | Account/profile/persona selection works in Desktop | Stable pass 63 baseline; not a defect. |
| SUP-2 | Already Addressed | Codex Desktop sees selected account/CODEX_HOME and selected CEM profile/persona/config | Stable pass 63 baseline; not a defect. |
| SUP-3 | Already Addressed | CLI Companion works with selected account/profile/workspace | Stable pass 63 baseline; not a defect. |
| SUP-4 | Already Addressed | Windows Terminal tab grouping works | Stable pass 63 baseline; not a defect. |
| SUP-5 | Already Addressed | Safe account deletion uses quarantine-first deletion | Stable pass 63 baseline; not a defect. |
| SUP-6 | Already Addressed | Usage Tracking and Burp Bridge are removed | These are descoped, not pending product features. |

## Needs Runtime Validation

These findings are plausible from static inspection or historical behavior, but they need live verification before they are classified as confirmed defects.

| ID | Status | Finding | What Needs Validation |
|---|---|---|---|
| P1-3 | Needs Runtime Validation | Best-effort Store-app desktop session tracking gap | This is partly an accepted limitation because Store app launch may use helper processes. Validate whether Active Sessions lies about an open session or prunes it immediately. The session model should distinguish `desktop-exe-tracked`, `desktop-store-helper`, and `desktop-untracked/best-effort`. This should not block account/profile functionality, because Desktop profile validation already works. |
| P1-5-R | Needs Runtime Validation | Session kill truthfulness after pass 55 | Validate the current kill/prune behavior on selected sessions after termination failures. The open question is not “can we kill sessions?” but “do we only remove a session after we know termination succeeded or after an explicitly accepted removal mode?” |
| P1-2-R | Needs Runtime Validation | Desktop launch failure points after state mutation | The current transaction boundaries need live validation so we can determine whether a failure after account/profile mutation leaves the system recoverable or confusing. |

## Documentation Drift

These items are not runtime defects by themselves, but the shipped documentation must match pass 63 reality.

| ID | Status | Finding | Recommended Update |
|---|---|---|---|
| P2-1 | Documentation Drift | Documentation out of sync | Update the README and related operator docs to reflect pass 63 architecture and runtime reality. Remove Usage/Burp references, avoid implying AGENTS.md contains a full role catalog, and document that Desktop workspace import remains under investigation. |

## Maintainability Gaps

These are valid improvements, but they are lower priority than launch safety and correctness.

| ID | Status | Finding | Recommendation |
|---|---|---|---|
| P2-2 | Maintainability Gap | No automated tests / CI | Add unit tests for PersonaEngine config/profile generation, launcher script generation, SessionManager kill/prune behavior, and ConfigService JSON corruption handling. Add a CI build check. |
| P2-3 | Maintainability Gap | PersonaEngine is too large / still carries dead legacy responsibilities | Some legacy helpers were intentionally kept during migration. Now that pass 63 is stable, remove dead descoped paths gradually: Burp managed-section constants if unused, old role-catalog AGENTS.md generation helpers if still present, and legacy managed block helpers if unused. Do not remove active profile instruction generation. |
## Remediation Order

### Priority A — next safety/correctness fixes

1. Harden batch escaping / launcher script generation.
2. Fix first-run settings/onboarding persistence.
3. Fix clearing Desktop path runtime override.
4. Validate/fix session kill truthfulness after pass 55.
5. Validate/fix invalid root profile reference.

### Priority B — Desktop reliability

1. Make Desktop launch more transactional.
2. Improve Store-app session tracking.
3. Continue Desktop workspace import investigation separately via deep-link/protocol/AppX discovery.

### Priority C — docs/refactor/tests

1. Update README to pass 63 reality.
2. Remove dead descoped Burp/Usage/legacy code.
3. Add tests/CI.
4. Split PersonaEngine later.

## Notes on Classification

- A static review should not convert an intentional design choice into a bug.
- A historical bug should not remain marked “confirmed” once later passes have clearly addressed it.
- A plausible risk that depends on launch mode, account type, or runtime environment should be labeled **Conditional Bug** or **Needs Runtime Validation** until proven.
- A feature that is intentionally removed should be documented as such instead of being recommended back into the product.
- The review should remain an operator/engineering document, not a generic vulnerability report.
