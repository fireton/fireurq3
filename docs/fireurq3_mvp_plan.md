# FireURQ3 MVP Plan

Version: 1.0  
Date: 2026-03-04  
Scope lock: DOS_URQ core subset only (no decorators in v1)

## 1. MVP Goals

Build a deterministic, headless URQL core on .NET 8 / C# 12 that can:

- Parse URQL source into AST with diagnostics.
- Compile AST into a simple IR.
- Execute IR with correct variable and control-flow semantics.
- Support runtime interpolation (`#...$`) with nesting and deterministic error handling.
- Support text output (`p`, `pln`) and action choice flow (`btn`) without UI.

## 2. Locked v1 Feature Set

Included commands/syntax:

- `:label`
- `end`
- assignment: `name = expr`
- `instr name = value`
- `if <expr> then <stmt-chain> [else <stmt-chain>]`
- `goto <labelExpr>`
- `proc <labelExpr>`
- statement chain separator: `&`
- `p <textExpr>`
- `pln <textExpr>`
- `btn <target>, <caption>`

Included language/runtime features:

- Case-insensitive identifiers/keywords.
- Single-line comments (`;`) and block comments (`/*...*/`).
- Logical line continuation using `_` at line start.
- Numeric/string/bool expression evaluation.
- Interpolation `#...$` nested and allowed anywhere in statement text.

Excluded from v1:

- Decorators and scene model.
- Inventory commands (`inv+`, `inv-`, etc.).
- save/load, audio/image, include, pause/anykey.
- Common-location advanced compatibility toggles.

## 3. Architecture

Pipeline:

1. `Lexer` -> tokens + trivia + source spans  
2. `Parser` -> immutable AST + diagnostics + recovery  
3. `Compiler` -> linear IR + label map + constant pools/templates  
4. `VM` -> deterministic execution with instruction budget  

Core modules:

- `Urql.Core.Diagnostics`
- `Urql.Core.Syntax` (tokens, AST, spans)
- `Urql.Core.Semantics` (command metadata, validation)
- `Urql.Core.Intermediate` (IR instructions)
- `Urql.Core.Runtime` (VM, Value, VariableStore, OutputBuffer, Buttons)

## 4. Interpolation Design (Priority)

Authoritative behavior:

- Syntax: `#<content>$`.
- `<content>` is expression-only.
- Nesting allowed; evaluate inner-to-outer.
- `#` and `$` are interpolation delimiters regardless of quote context.

Implementation contract:

- `ExpandInterpolations(string input, EvalContext ctx) -> string`
- Stack-based parser (no regex).
- Compile expression fragments once into expression AST/IR templates.

Deterministic error policy:

- Unmatched `#` or `$` => diagnostic with line/column.
- Failed interpolation expression => diagnostic + empty substitution.
- Continue execution deterministically.

Safety:

- Max expansion passes per statement: 16.

Post-expansion execution:

- Expanded result parsed as exactly one statement.
- Reject trailing tokens.
- Restricted dynamic mode forbids structural constructs (`if`, labels, `goto`, `proc`, `end`).
- Allowed dynamic forms in v1: assignment, `instr`, `p`, `pln`.

## 5. Runtime Execution Model

State:

- Instruction pointer
- Variable store
- Proc return stack
- Output buffer
- Current button list
- VM status

VM statuses:

- `Running`
- `WaitingForChoice` (after top-level `end` with one or more buttons)
- `Halted`
- `Faulted`

Button flow:

- `btn target, caption` appends a button action to current pending list.
- On top-level `end`, VM enters `WaitingForChoice` if buttons exist.
- Host resumes via `ChooseButton(id)`; VM jumps to button target.

Host API:

- `Step()`
- `RunUntilWaitOrHalt(maxInstructions)`
- `ChooseButton(buttonId)`
- `RunUntilHalt(maxInstructions)`

## 6. Data and Type Semantics

Value kinds:

- Number (`double`)
- String
- Bool

Conversions:

- String -> Number = string length
- Number -> String = empty string (compatibility default)
- Truthiness: `0`/empty/false are false

Undefined variable reads:

- Numeric context: `0`
- String context: `""`

## 7. IR Plan

Instruction families:

- Load/store/eval: `LoadConst`, `LoadVar`, `StoreVar`, arithmetic/logic ops
- Flow: `Jump`, `JumpIfFalse`, `Call`, `ReturnOrHalt`
- Output: `Print`, `PrintLn`
- Actions: `AddButton`, `WaitForChoice`
- Interpolation/dynamic: `ExpandTemplate`, `ExecDynamicSingle`
- Meta: `Diag`, `Nop`

## 8. Project/File Plan

Planned workspace structure:

- `/src/Urql.Core/`
- `/tests/Urql.Core.Tests/`
- `/Spec.md`
- `/README.md`
- `/fireurq3_mvp_plan.md` (this document)

High-level initial files:

- `Lexer.cs`, `Token.cs`, `TokenKind.cs`
- `Parser.cs`, AST node files
- `Diagnostic.cs`, `DiagnosticBag.cs`
- `IrInstruction.cs`, `Compiler.cs`
- `Vm.cs`, `Value.cs`, `VariableStore.cs`, `OutputBuffer.cs`, `ButtonAction.cs`
- `InterpolationExpander.cs`, template node files

## 9. Milestones

### M1: Spec Alignment and Skeleton

Deliverables:

- Update `Spec.md` to match final v1 scope.
- Create solution/projects and namespace skeleton.

Exit criteria:

- Builds successfully.
- No runtime features yet.

### M2: Lexer + Diagnostics

Deliverables:

- Tokenizer with comments, continuation, spans.
- Diagnostic emission for lexical errors.

Tests:

- Token snapshots.
- Bad token diagnostics with exact line/column.

### M3: Parser + AST

Deliverables:

- Parse all v1 constructs.
- Error recovery to next line/statement.

Tests:

- AST snapshots (golden).
- Syntax error recovery cases.

### M4: Expression Engine + Interpolation Templates

Deliverables:

- Expression parser/evaluator compatibility rules.
- Template AST (`LiteralText|EmbeddedExpression`).
- `ExpandInterpolations` stack-based implementation.

Tests:

- Nested interpolation cases.
- Interpolation inside quoted text.
- Unmatched delimiters diagnostics.

### M5: IR Compiler + VM Core

Deliverables:

- AST-to-IR compiler.
- VM with step/run budget and statuses.
- `goto/proc/end` behavior.

Tests:

- Control flow and call-return sequences.
- Infinite loop budget cutoff.

### M6: Output + Button Flow

Deliverables:

- `p/pln` output buffer support.
- `btn` queue and `WaitingForChoice` resume flow.

Tests:

- Output assertions.
- Button selection transitions.

### M7: Dynamic Single-Statement Execution

Deliverables:

- Post-interpolation restricted statement execution mode.
- Structural-command rejection in dynamic mode.

Tests:

- Allowed dynamic statement executes.
- Forbidden dynamic statement emits diagnostic and no-op.

### M8: Hardening and Docs

Deliverables:

- README run/test instructions + sample.
- Final pass over diagnostics and deterministic behavior.

Tests:

- Full suite green.
- Snapshot stabilization.

## 10. Test Strategy

Test categories:

- Unit: lexer/parser/expression/interpolation.
- Golden: AST and IR snapshot tests.
- Execution: end-to-end scripts with expected variables/output/buttons/state.
- Negative: malformed input, unmatched interpolation delimiters, unknown labels.

Determinism controls:

- No global static mutable state.
- Culture-invariant numeric parsing/formatting.
- Instruction budget enforced in all run methods.

## 11. Risks and Mitigations

Risk: interpolation complexity and nesting corner cases.  
Mitigation: stack parser + focused property-style tests + pass cap.

Risk: legacy semantic ambiguities.  
Mitigation: explicit assumptions in `Spec.md` + targeted compatibility tests.

Risk: parser fragility with chained statements and mixed legacy syntax.  
Mitigation: simple grammar, permissive recovery, extensive malformed-input tests.

## 12. Done Criteria for MVP

MVP is complete when:

- All locked v1 commands parse, compile, and execute.
- Interpolation behaves per section 4 in tests.
- VM supports waiting/resume choice flow via buttons.
- Diagnostics are source-located and user-facing text is English.
- `dotnet test` is green for all planned suites.
