# Chat V2 Manual Migration Runbook

Manual-only. No automatic database update command.

## File to run in DBeaver

- `backend/SqlScripts/20260404_chat_v2_manual_all_in_one.sql`

## What the script does

1. Expand schema
2. Backfill data
3. Validate migration data
4. Contract schema (only if validation passes)

If validation fails, script throws error and contract step is skipped.

## Safety

- Take backup/restore point before running.
- Run on staging first, then production.
