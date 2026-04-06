# AGENTS.md — FireURQ3 Project Rules

This file defines project-specific implementation and collaboration rules for agents working in this repository.

## 1. Project Goal

Build a headless URQL interpreter core that is DOS_URQ-compatible by default and extensible toward FireURQ behavior.

- No UI/renderer in core scope.
- Deterministic runtime behavior is required.
- Keep dependencies minimal.
- Strategic direction: compatibility-first with legacy quests, but this is a new URQL implementation intended to be significantly extensible beyond DOS_URQ.
- Active development target: a TypeScript web application is now the primary platform for the project.
- `.NET` is now a migration source, not the long-term runtime target.
- During migration, preserve deterministic core behavior and compatibility semantics while moving implementation into the web stack.
- After the migration is complete, the legacy `.NET` project is intended to be removed from the repository.
- Web player layout should stay close to the classic FireURQ2 standard skin where practical:
  - top menu bar at the top of the viewport,
  - transcript pane occupying the remaining main area below it.
- Current player target layout:
  - the transcript pane should be flush with the edges of the virtual `800x600` viewport below the top menu bar,
  - avoid decorative inner framing that reduces usable transcript area.
  - visual styling may be modernized, but the spatial layout should stay as close to FireURQ2 as practical for legacy quest compatibility.

## 2. Source of Truth

When behavior is unclear, use this priority:

1. User decisions in current thread.
2. `docs/Spec.md`.
3. Original docs in `docs/URQL.txt` and `docs/FireURQ_Особенности реализации URQL — IFВики.html`.

If implementation changes behavior, update `docs/Spec.md` and `README.md` in the same task.

## 3. Language/Compatibility Principles

- DOS_URQ behavior is the default behavior, not an optional legacy mode.
- Keep parser permissive for real quests by default.
- Preserve backward compatibility for old quests whenever practical.

## 4. Parsing Rules

- Variable names may contain spaces.
  - Example: `мы поели = 1` defines variable name `мы поели`.
- Label names may contain spaces.
  - Example: `:use_Топор_Рубить дерево` is valid.
- DOS line continuation is supported:
  - physical lines starting with optional spaces/tabs then `_` continue previous logical line.
- `p/pln/print/println` parse DOS-style raw line tail text.
  - Literal quotes in print raw tails are preserved; `pln "done"` outputs `"done"`, not `done`.
- `btn` parses DOS-style raw target and raw caption tails.

## 5. Runtime/VM Rules

- Deterministic stepping and run limits are mandatory.
- `btn` adds choices; wait state occurs on top-level `end` when buttons exist.
- `proc`/`end` is call/return; top-level `end` halts or waits for button selection.

## 6. Inventory Rules

Implemented core rules:

- Commands: `inv+`, `inv-`, `invkill`, `perkill`.
- Inventory item keys are case-insensitive.
- Default `inv+`/`inv-` count is `1`.
- Resulting count `<= 0` removes item.
- `inv_<item>` is read/write bridge to item count.
- Bare identifier fallback in expressions:
  - if normal variable is missing, matching inventory item count is used.
  - enables checks like `if not Веревка then ...`.

## 7. Use-Action Flow

- Support `use_...` labels via VM host APIs.
- `UseInventoryItem(item[, action])` and `InvokeUseLabel(label)` execute use-label as proc-like call.
- On `end`, control returns to prior context.
- If invoked while waiting for choice, returns to `WaitingForChoice` after use-label return.

## 8. Interpolations

Supported:

- Nested `#...$` anywhere (including inside quotes), stack-based parsing.
- `#%name$` string interpolation path.
- `#$` -> space.
- `#/$` -> newline.
- `##NN$` -> character by code, encoding-dependent:
  - UTF quest encoding: `NN` is Unicode code point.
  - `cp1251/cp866/koi8-r`: `NN` is decoded as byte in that codepage.

Expansion/execution model:

- Expand at runtime before statement execution.
- Dynamic single-statement execution is allowed after expansion.
- Must parse exactly one statement and reject statement chains/labels.
- `if ... then ... else ...` is disallowed in dynamic single-statement mode.

## 9. Unsupported Commands Policy

Two parser modes via `ParserOptions.AllowUnknownCommands`:

- `true` (default, permissive): unsupported command -> warning + no-op.
- `false` (strict): unsupported command -> error + no-op.

Applies to normal unknown commands and `%...` macro-like lines (for example `%include ...`).

## 10. Encoding Rules

- Loader supports `utf-8`, `cp1251`, `cp866`, `koi8-r`, plus auto-detect.
- `windows-1252` is intentionally unsupported.
- Runtime interpolation char-code behavior must use encoding selected from quest load result.

## 11. Code/Docs Maintenance Rules

After each milestone or behavior change:

1. Update tests.
2. Run test suite.
3. Update `docs/Spec.md` when semantics changed.
4. Update `README.md` with concise progress/status notes.

Do not leave docs stale relative to implementation.

Rule capture:

- When the user introduces a new project rule or decision, add it to `AGENTS.md` in the same task (unless user explicitly says not to).

## 12. Quality Bar

- Add/keep unit tests for lexer/parser/runtime changes.
- Prefer focused tests with behavior assertions over broad snapshots when possible.
- No user-facing messages in non-English.
- Keep comments in code in English.

## 13. Development Stack

- Always use pnpm instead of npm for terminal commands and dependency management.