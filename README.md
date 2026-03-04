# FireURQ3

Headless URQL core runtime prototype on .NET 8 / C# 12.

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

## Projects

- `src/Urql.Core` - core library (syntax, IR, runtime)
- `src/Urql.Runner` - minimal CLI runner for manual smoke checks
- `tests/Urql.Core.Tests` - unit/golden/execution tests

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

The runner currently prints:

- token count + lexer diagnostics count
- parsed line count + parser diagnostics count
- VM status summary

The runner also applies detected quest encoding to `##NN$` interpolation decoding through runtime context.

`##NN$` behavior:

- for `cp1251` / `cp866` / `koi8-r`: `NN` is treated as byte `0..255` and decoded via that codepage
- for UTF sources (`utf-8`, `utf-16*`): `NN` is treated as Unicode code point

## Documentation

- Spec: `docs/Spec.md`
- MVP plan: `docs/fireurq3_mvp_plan.md`
- Source references:
  - `docs/URQL.txt`
  - `docs/FireURQ_Особенности реализации URQL — IFВики.html`
