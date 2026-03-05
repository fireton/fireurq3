# Official Player Design

## Goal

Build the official FireURQ player on MonoGame with a compatibility layer that keeps `Urql.Core` headless and deterministic.

## Modules

- `Urql.Core`: parser/compiler/VM.
- `Urql.Player.Compat`: skin loading, rich text/link parsing, virtual viewport mapping, immutable frame snapshots.
- `Urql.Runner.MonoGame`: rendering, input, OS integrations.

## Data Flow

1. Load quest text and compile to VM (`Urql.Core`).
2. Resolve skin (`skin.xml`/`skin.json`/built-in default).
3. Run VM to wait/halt and collect output delta.
4. Parse output text to rich runs (links and text runs).
5. Build `PlayerFrame` including virtual-space transform and interactives.
6. Render `PlayerFrame` in MonoGame.

## Compatibility Scope (current)

- Legacy skin XML structure parsing.
- Built-in default skin fallback.
- 800x600 virtual game-space + letterbox mapping.
- Basic `[[text|target]]`, `[[text]]`, `%menu`, `!local` link parsing.

## Deferred

- Decorator runtime.
- Full menu execution parity.
- Advanced media subsystem parity.
