# MyGarage Working Invariants

These rules are non-negotiable for all JSX/UI updates in this repository.

## 1) UTF-8 Strictness

- Always use tools that preserve UTF-8 when editing JSX/CSS/TS/JS files.
- After each layout/UI edit, run `git diff` and verify:
- Only structure/class/style changes are present when intended.
- Vietnamese user-facing text must remain unchanged unless explicitly requested.
- If any mojibake/encoding corruption appears, stop and fix encoding before continuing.

## 2) UI/Content Separation

- Keep instructional text separate from render structure and business logic.
- Prefer constants/variables/config/tooltip labels for UI guidance text.
- Do not hardwire transient guidance text deep inside data-processing branches.
- Changing visual behavior (show/hide/mode) must not require touching core data logic.

## 3) Safety Check Before Finalizing

- Re-read modified JSX blocks to ensure:
- No accidental text mutation.
- No accidental encoding escapes or broken Vietnamese diacritics.
- No mixed concern between layout and content logic.

## 4) Execution Discipline (Mandatory)

- Read `AGENTS.md` before making any code change.
- Re-read `AGENTS.md` every 5 minutes while actively working.
- Do not change anything outside the exact user request.
- Never run `git clone`, `git pull`, or any equivalent auto-sync/auto-fetch action during autonomous execution unless explicitly requested by the user in that turn.

## 5) Domain Invariants

- Diagnosis recommendations must flow through Quotations; direct links to SO are strictly prohibited.
- Relational discounts are the single source of truth; legacy `TotalDiscount` columns must not be used as business source.
