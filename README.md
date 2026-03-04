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

Not implemented yet:

- inventory commands
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

## Documentation

- Spec: `docs/Spec.md`
- MVP plan: `docs/fireurq3_mvp_plan.md`
- Source references:
  - `docs/URQL.txt`
  - `docs/FireURQ_Особенности реализации URQL — IFВики.html`
