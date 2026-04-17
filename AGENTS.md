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

