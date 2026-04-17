# Git Hooks

This repository uses a pre-commit hook to detect likely mojibake/encoding corruption in added lines.

## Enable hooks (local machine)

- PowerShell: `./scripts/setup-git-hooks.ps1`
- Bash: `sh ./scripts/setup-git-hooks.sh`

## Temporary bypass (when really needed)

- `SKIP_MOJIBAKE_CHECK=1 git commit -m "..."` (bash)
- `$env:SKIP_MOJIBAKE_CHECK='1'; git commit -m "..."` (PowerShell)
