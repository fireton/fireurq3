# Hamster Quests Compatibility Plan

Date: 2026-03-04  
Target files:

- `tests/quests/hamster1.qst`
- `tests/quests/hamster2.qst`

Goal: run both quests end-to-end in headless mode, walk by buttons, and verify expected outcomes.

## Step 1: Parser Compatibility Mode (DOS_URQ-like)

Implement parser/runtime compatibility mode to handle real-world quest syntax:

- Add parser option `CompatibilityMode = DosUrq`.
- In this mode:
  - `p/pln/print/println` parse raw text tails (not expression-only).
  - `btn target, caption` parses target + caption robustly from line tail.
  - Unknown commands become warning + no-op statements (not hard parse failure).

Expected outcome:

- Significant reduction of diagnostics on legacy quest files.
- Quests can at least parse and reach first playable state.

## Step 2: Missing Commands Used by Hamster Quests

Implement minimal command set used in these quests:

- `perkill`
- `invkill`
- `inv+ [count,] item`
- `inv- [count,] item`
- `inv_item` read/write bridge

Expected outcome:

- State transitions and item-based conditions work in gameplay paths.

## Step 3: Inventory Semantics

Add inventory runtime model:

- case-insensitive item keys
- default count increment/decrement behavior
- clamp to zero
- bare item truthiness support in expressions/conditions

## Step 4: Interpolation Legacy Extensions

Complete legacy interpolation forms used by old quests:

- `#$` -> space
- `#/$` -> newline
- `##NN$` -> character code

Ensure operation in print and button text paths.

## Step 5: Diagnostics Policy for Legacy Quests

Add permissive compatibility behavior:

- unsupported commands: warning + no-op
- hard stop only on fatal parser/runtime issues

## Step 6: Scenario Harness for Button Walkthroughs

Create quest scenario tests:

- path to quest file
- encoding mode (`auto`)
- sequence of button picks (caption/index)
- checkpoints and final assertions

Assertions include:

- VM status
- variable/inventory values
- output fragments
- diagnostics class (fatal vs warning)

Status: implemented (harness + sample scenario tests).

## Step 7: Add Real Scenarios

Create:

- `hamster1.walk.json`
- `hamster2.walk.json`

At least two scenarios per quest:

- happy path
- failure/death branch

## Step 8: Acceptance Criteria

- Both quests load and start menu is reachable.
- Scripted button walks run without VM fault.
- Expected end-state assertions pass.
- Remaining diagnostics are non-fatal compatibility warnings.
