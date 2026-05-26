# Implementation Report - Kimi launch hardening and docs alignment

## Files Changed

### Modified
- `README.md`
- `Services/KimiCliManager.cs`

### New / Updated
- `IMPLEMENTATION_REPORT.md`

## Summary

This checkpoint focuses on stabilizing the Kimi launch path and aligning the README with the current
codebase.

Key changes:

- Kimi login/setup now sets both `KIMI_CODE_HOME` and `KIMI_SHARE_DIR` for account isolation.
- Kimi launch scripts now set:
  - `KIMI_CODE_HOME`
  - `KIMI_SHARE_DIR`
  - `PYTHONIOENCODING=utf-8`
  - `PYTHONUTF8=1`
  - UTF-8 code page via `chcp 65001`
- The Kimi PowerShell wrapper now invokes the CLI directly with:
  - `& $kimiPath @kimiArgs`
- README now reflects:
  - Codex and Kimi provider support
  - Codex Desktop profile override behavior
  - Active Sessions requested-profile tracking and profile-override status
  - Kimi CLI-only behavior with `--work-dir`, `--agent-file`, and `--model`
  - Kimi optional controls currently implemented
  - AGENTS.md compact guidance / role-template source of truth
  - Usage and Burp Bridge being descoped

## Validation

### Build

```powershell
E:\dotnet-sdk\dotnet.exe build -c Debug -nr:false -p:UseSharedCompilation=false
```

Result: build succeeded.

### Tests

```powershell
E:\dotnet-sdk\dotnet.exe run --project Tests\CodexEnvironmentManager.Tests\CodexEnvironmentManager.Tests.csproj -c Debug
```

Result: all tests passed.

## Remaining Risks

- The Kimi launch path still depends on the external Kimi CLI honoring the documented environment
  variables and `--agent-file`/`--work-dir` contract.
- Desktop workspace binding remains best-effort where the app can only verify the Store/AppX launch
  path indirectly.

## Notes

- Transient `bin/` and `obj/` outputs were removed before the checkpoint commit.
- The repository should be committed only after a clean `git status --short` confirms no build
  artifacts remain.
