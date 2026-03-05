# FireURQ3

Headless URQL core runtime + runner prototypes on .NET 8 / C# 12.

Current implemented pipeline:

- Lexer with diagnostics
- Parser + immutable AST + recovery
- AST -> IR compiler
- IR virtual machine (core DOS_URQ subset)
- Runtime interpolation expansion (`#...$`, nested, stack-based)
- Dynamic single-statement execution mode
- xUnit test suite

## Milestone Progress

- Step 1: DOS-style parser behavior (raw line tails, unknown-command no-op warnings) - done
- Step 2: inventory command set (`perkill`, `invkill`, `inv+`, `inv-`, `inv_...`) - done
- Step 3: inventory semantics + `use_...` label invocation/return flow - done
- Step 4: special interpolation forms (`#$`, `#/$`, `##NN$`) - done
- Step 5: permissive diagnostics policy for unsupported commands - done
- Strict diagnostics mode for unsupported commands - done
- Step 6: scenario harness for quest walkthroughs - done
- Step 7: real hamster walkthrough scenarios - done
- Step 8: acceptance checks for scripted quest runs - done
  - includes hamster1 winning ending scenario (fanfare escape)
  - includes hamster2 winning ending scenario (fanfare descent)

## Projects

- `src/Urql.Core` - core library (syntax, IR, runtime)
- `src/Urql.Player.Compat` - compatibility layer (skin loading, rich text links, virtual viewport mapping)
- `src/Urql.Runner` - minimal CLI runner for manual smoke checks
- `src/Urql.Runner.MonoGame` - desktop MonoGame runner with console-like transcript flow
- `tests/Urql.Core.Tests` - unit/golden/execution tests
- scenario harness tests: file-based quest run + scripted button walks + checkpoints

## Scope (current)

Implemented commands:

- `:label`
- `end`
- assignment (`a=expr`)
- `instr`
- `if ... then ... else ...`
- `goto`
- `proc`
- `p` / `print`
- `pln` / `println`
- `btn`
- `perkill`
- `invkill` (all or item)
- `inv+ [count,] item`
- `inv- [count,] item`

Implemented runtime bridges/behavior:

- `inv_<item>` read/write bridge
- bare inventory item checks in expressions/conditions (e.g. `if not Веревка then ...`)
- `use_...` label invocation API with proc-like return
- unsupported commands are parsed as warning + no-op (including `%include`-style macro lines)

Not implemented yet:

- save/load
- pause/anykey/input
- decorators/scene rendering
- `forget_procs`

## Build

```bash
cd /Users/fireton/fieurq3
dotnet build FireURQ3.sln
```

## Test

```bash
cd /Users/fireton/fieurq3
dotnet test tests/Urql.Core.Tests/Urql.Core.Tests.csproj
```

If your environment has restricted network, use cached NuGet packages:

```bash
cd /Users/fireton/fieurq3
NUGET_PACKAGES=/Users/fireton/.nuget/packages dotnet test tests/Urql.Core.Tests/Urql.Core.Tests.csproj --ignore-failed-sources
```

## Runner

Run parser/compiler/vm smoke on a script file:

```bash
cd /Users/fireton/fieurq3
dotnet run --project src/Urql.Runner/Urql.Runner.csproj /path/to/script.qst
```

Strict diagnostics mode (unsupported commands are errors):

```bash
cd /Users/fireton/fieurq3
dotnet run --project src/Urql.Runner/Urql.Runner.csproj /path/to/script.qst --strict
```

The runner currently prints:

- token count + lexer diagnostics count
- parsed line count + parser diagnostics count
- VM status summary

The runner also applies detected quest encoding to `##NN$` interpolation decoding through runtime context.

Run the MonoGame desktop runner with console-like flow:

```bash
cd /Users/fireton/fieurq3
dotnet run --project src/Urql.Runner.MonoGame/Urql.Runner.MonoGame.csproj /path/to/script.qst
```

If no quest path is passed, the runner opens an OS-native file chooser dialog.

Official-player mode defaults:

- legacy compatibility profile: `FireUrqLegacy`
- skin fallback: quest `skin.xml` / `skin.json` -> built-in `Assets/Skins/default/skin.xml`
- virtual game-space: skin-defined size (default `800x600`)
- resizable window via letterboxed aspect-preserving view mapping

MonoGame runner controls:

- `Up` / `Down` - move active choice
- `Enter` - confirm selected choice
- Mouse hover + click - select a choice
- `Esc` - exit

MonoGame runner behavior:

- transcript output is append-only and auto-scrolls to the tail
- active choices appear inline after transcript text
- after selection, choices are removed and echoed as `[Caption]` in transcript
- bundled fonts from `src/Urql.Runner.MonoGame/Assets/Fonts` are used first, then system fonts are fallback

`##NN$` behavior:

- for `cp1251` / `cp866` / `koi8-r`: `NN` is treated as byte `0..255` and decoded via that codepage
- for UTF sources (`utf-8`, `utf-16*`): `NN` is treated as Unicode code point

## Documentation

- Spec: `docs/Spec.md`
- MVP plan: `docs/fireurq3_mvp_plan.md`
- MonoGame runner plan: `docs/quest_runner_monogame_plan.md`
- Official player design: `docs/official_player_design.md`
- Skin compatibility map: `docs/skin_compatibility.md`
- Skin JSON v1: `docs/skin_schema_v1.md`
- Virtual viewport policy: `docs/virtual_viewport_policy.md`
- Source references:
  - `docs/URQL.txt`
  - `docs/FireURQ_Особенности реализации URQL — IFВики.html`
