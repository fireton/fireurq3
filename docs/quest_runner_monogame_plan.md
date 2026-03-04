# Quest Runner Plan (MonoGame, Console-Like Flow)

## Summary

Build a MonoGame desktop runner that behaves like a console transcript, not split panels.
Text is rendered in one continuous flow. When choices are available, buttons are shown inline after the current text block. After selection, choice controls disappear and the selected caption is appended to transcript as `[Caption]`.

## Scope

### In scope (M1)

1. New project: `src/Urql.Runner.MonoGame` (`net8.0` desktop).
2. Load `.qst` via existing `UrqlTextLoader`.
3. Parse/compile/run through existing `VirtualMachine`.
4. Single transcript view (console-like scroll).
5. Inline choice block rendered at transcript tail.
6. Choice echo line format: `[<selected caption>]`.
7. Keyboard/mouse choice support.
8. Diagnostics/status overlay (non-intrusive).

### Out of scope (M1)

1. Mobile targets (Android/iOS).
2. Web/browser target.
3. Decorators/sprites/scene system.
4. Save/load UX.
5. Core semantics changes in `Urql.Core`.

## UX Contract (Locked)

1. Main surface is one vertical scrollable transcript.
2. VM output (`p/pln`) appends to transcript exactly in order.
3. When VM enters `WaitingForChoice`:
   1. Render choice list immediately after transcript tail.
   2. Choices are interactive (keyboard/mouse).
4. On user choice:
   1. Append one transcript line: `[Caption]`.
   2. Remove choice UI immediately.
   3. Execute `ChooseButton(id)` and continue VM.
5. Old choices must never remain interactive after selection.

## Architecture

1. Keep `Urql.Core` headless and unchanged.
2. Add runner-side orchestration:
   1. `QuestSession` (VM lifecycle + deterministic stepping).
   2. `TranscriptBuffer` (append-only lines/events).
   3. `ChoicePresenter` (current visible choices only).
3. MonoGame renderer draws from immutable `RunnerViewModel` snapshot each frame.

## Data Model Additions (Runner Project)

1. `TranscriptEntry`
   1. `Kind` (`Output`, `ChoiceEcho`, `System`)
   2. `Text`
   3. `TimestampTicks` (for debug ordering)
2. `RunnerViewModel`
   1. `IReadOnlyList<TranscriptEntry> Transcript`
   2. `IReadOnlyList<ButtonAction> ActiveChoices`
   3. `VmStatus Status`
   4. `bool HitInstructionLimit`
   5. `IReadOnlyList<Diagnostic> Diagnostics`
3. `QuestSession` API
   1. `Load(path, config)`
   2. `Advance()`
   3. `SelectButton(buttonId)` (must append `[Caption]` before continuing)
   4. `Snapshot()`

## Rendering/Input Rules

1. Transcript text uses Unicode-capable runtime font rendering (Cyrillic-safe).
2. Choice block style is visually distinct but inline with transcript flow.
3. Keyboard mapping:
   1. `Up/Down` move selected choice.
   2. `Enter` confirm.
4. Mouse mapping:
   1. Hover highlights choice.
   2. Click confirms.
5. Scroll:
   1. Auto-scroll to bottom on new output/choice echo.
   2. Manual scroll lock can be added later; default follows latest output.

## Determinism Guarantees

1. VM advances only in `Advance()` or `SelectButton()`.
2. Draw/update FPS never changes VM behavior.
3. Choice ordering matches VM insertion order.
4. Echo text uses exact chosen caption string from `ButtonAction`.

## Tests

### Unit (runner logic)

1. Entering `WaitingForChoice` exposes active choices at transcript tail.
2. `SelectButton` appends exact `[Caption]` echo.
3. After selection, `ActiveChoices` becomes empty until next wait state.
4. Instruction-limit hit is surfaced in snapshot.

### Integration

1. Run hamster quests through `QuestSession`; verify transcript checkpoints.
2. Verify echo entries appear at correct moments in flow.

### Manual acceptance

1. Play path entirely with keyboard; transcript shows bracketed echoes.
2. Repeat with mouse.
3. Confirm choices disappear after each selection.
4. Confirm Cyrillic rendering quality.

## Documentation Changes

1. Keep this plan in `docs/quest_runner_monogame_plan.md`.
2. Update `README.md` with MonoGame runner run instructions and controls.
3. Update `docs/Spec.md` only if core semantics change (not required in this task).

## Assumptions

1. M1 target is desktop only.
2. Console-like transcript UX is mandatory (no two-panel UI).
3. Echo format is fixed as `[Caption]` for now.
