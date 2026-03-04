# URQL Core Spec (DOS_URQ Subset, Core Runtime MVP)

Version: 0.2 (derived)
Target runtime: .NET 8, C# 12
Scope: Headless parser/compiler/VM core for DOS_URQ basics (no UI/rendering)

## 1. Goals and Scope

This specification defines a concrete, deterministic subset and execution model for a new URQL core runtime, derived from:

- DOS_URQ documentation (URQ_DOS 1.35 era)
- FireURQ implementation notes

The core must support:

- Parsing scripts into AST
- Compiling AST into IR
- Executing IR in a deterministic VM
- Variable semantics (numeric/string/bool)
- Label-based control flow
- Text output commands (`p`, `pln`)
- Button/action command (`btn`) with host-driven choice
- Diagnostics with source location

Out of scope for this stage:

- Rendering, audio, input devices, persistence UI
- Full legacy command set
- FireURQ-specific multimedia/decorator features

## 2. Source Model and Encoding

- Input is a sequence of lines in text files (`.qst`-like).
- Engine internally uses UTF-8/Unicode strings.
- Keywords and identifiers are case-insensitive for matching.
- Original spelling/casing may be preserved for diagnostics/debug display.

## 3. Lexical Rules

## 3.1 Whitespace

- Spaces and tabs separate tokens.
- Newline terminates a statement line, unless line continuation applies.

## 3.2 Line Continuation

- If first non-whitespace character of a line is `_`, this line is concatenated to the previous logical line.
- The `_` marker is removed; one single space is inserted at the join.
- This matches DOS_URQ style “line is continuation of previous”.

## 3.3 Comments

- Single-line comment: `;` to end of logical line.
- Block comment: `/* ... */`, non-nesting.
- Comments are removed before parsing statements.

## 3.4 Identifiers

- Identifier chars: letters, digits, underscore.
- First character: letter or underscore.
- Canonical form for lookup: lower-invariant.
- Label names follow identifier rule for MVP.

## 3.5 Literals

- Numeric:
  - Integer: `123`
  - Float: `123.45`
  - Hex (FireURQ-style for colors): `0xAARRGGBB` or `0xRRGGBB`
- String:
  - Double-quoted: `"text"`
  - Escapes (assumption): `\\`, `\"`, `\n`, `\r`, `\t`, `\xNN`
  - Unescaped text in some command tails is allowed by command grammar (legacy-like), but parser normalizes into string nodes where applicable.

## 4. Script Structure

- A script is a set of labeled blocks and top-level statements.
- Label declaration syntax:
  - `:<labelName>`
- Execution starts at first declared label in source order.
- `end` marks end-of-block in control-flow context.

Notes:

- Multiple identical labels are legal in legacy engines; first wins.
- Core behavior: emit warning for duplicate label and bind jumps to first occurrence.

## 5. Core Grammar (EBNF-ish)

```ebnf
script          := { statementLine } ;

statementLine   := [ labelDecl ] [ statementChain ] newline ;
labelDecl       := ":" identifier ;

statementChain  := statement { "&" statement } ;

statement       := assignment
                 | instrAssign
                 | ifStatement
                 | gotoStatement
                 | procStatement
                 | endStatement
                 | printStatement
                 | printlnStatement
                 | btnStatement
                 | commandCall ;   // extensibility fallback

assignment      := identifier "=" expr ;
instrAssign     := "instr" identifier "=" stringExpr ;

ifStatement     := "if" expr "then" statementChain [ "else" statementChain ] ;

gotoStatement   := "goto" targetRef ;
procStatement   := "proc" targetRef ;
endStatement    := "end" ;
printStatement  := ("p" | "print") interpolatedText ;
printlnStatement:= ("pln" | "println") interpolatedText ;
btnStatement    := "btn" targetRef "," interpolatedText ;

targetRef       := interpolatedText ;

expr            := orExpr ;
orExpr          := andExpr { "or" andExpr } ;
andExpr         := unaryExpr { "and" unaryExpr } ;
unaryExpr       := [ "not" | "-" | "+" ] unaryExpr | compareExpr ;
compareExpr     := addExpr [ compOp addExpr ] ;
compOp          := "=" | "<>" | "<" | ">" | "<=" | ">=" | "==" ;
addExpr         := mulExpr { ("+" | "-") mulExpr } ;
mulExpr         := primary { ("*" | "/" | "%") primary } ;
primary         := number | string | identifier | "(" expr ")" ;

stringExpr      := expr ; // runtime coercion to string as needed
```

## 6. Value Types and Conversions

Runtime value kinds:

- `Number` (double)
- `String`
- `Bool`
- `Null` (internal only; reading undefined variable yields type-dependent default, see below)

Conversion rules:

- Number -> String: empty string (`""`) for strict DOS_URQ/FireURQ compatibility.
- String -> Number: string length (FireURQ rule).
- Bool -> Number: `1`/`0`.
- Bool -> String: `"1"`/`"0"` (assumption for diagnostics/log simplicity).

Truthiness:

- Number: false if `0`, true otherwise.
- String: false if empty, true otherwise.
- Bool: as is.

Undefined variables:

- Numeric context: `0`
- String context: `""`
- Bool context: false

## 7. Expression Semantics

- Arithmetic (`+ - * / %`) operates on numeric projections.
- `+` string concatenation:
  - If left operand is String, concatenate string projections.
  - Else numeric addition.
  - This matches FireURQ “string + string/text” behavior while preserving numeric default.
- `=`:
  - If either operand is String in source expression context, compare string projections.
  - Else numeric compare.
- `<>`: logical not equal.
- `==`:
  - Wildcard string match using `*` and `?`.
  - Left operand treated as tested string.
  - Right operand treated as wildcard pattern string.
  - Case-insensitive match.

Operator precedence (high -> low):

1. unary (`not`, unary `+`, unary `-`)
2. multiplicative (`*`, `/`, `%`)
3. additive (`+`, `-`)
4. comparisons (`=`, `<>`, `<`, `>`, `<=`, `>=`, `==`)
5. `and`
6. `or`

## 8. Interpolation Model (Critical)

This section defines authoritative runtime interpolation for URQL core.

## 8.1 Syntax and placement

- Interpolation syntax: `#<content>$`.
- `<content>` is parsed as an expression, not as a statement.
- Interpolations are allowed anywhere in a statement text, including inside quoted strings.
- Parser treats `#` as open and `$` as close regardless of quoting mode.

## 8.2 Nesting and expansion order

- Interpolations may be nested.
- Expansion order is inner-to-outer.
- Expansion algorithm must be stack-based (not regex-based) to support proper nesting.

Required API:

- `ExpandInterpolations(string input, EvalContext ctx) -> string`

## 8.3 Runtime timing

- Interpolations are expanded at runtime before executing the target statement.
- Expansion result of each `#...$` is converted to string using URQL coercion rules and inserted into surrounding text.
- Expansion is repeated until no interpolation markers remain in the statement, or a safety cap is reached.

## 8.4 Error handling and determinism

- Unmatched `#` or `$` is a diagnostic error with source line/column.
- On interpolation parse/eval failure, execution remains deterministic:
  - Emit diagnostic.
  - Substitute empty string for failed interpolation segment.
  - Continue expansion/execution flow.

Safety cap:

- Maximum interpolation expansion passes per statement: 16 (assumption).

## 8.5 Post-expansion execution mode (single-statement dynamic execution)

After expansion, resulting text is parsed/executed as exactly one URQL statement in a restricted mode:

- Parse exactly one statement.
- Reject trailing tokens.
- Allow only non-structural commands.
- Reject structural constructs (`if`, labels, `goto`, `proc`, `end`, loops, returns, and any future control-flow forms).

If restricted-parse fails:

- Emit diagnostic.
- Statement is treated as no-op.

This guarantees interpolation cannot synthesize control flow or alter program structure.

## 8.6 AST and IR representation

Interpolated text arguments are represented as templates:

- `LiteralText`
- `EmbeddedExpression`

Compilation rule:

- Embedded expression parts are parsed into expression AST and compiled into expression IR once.
- Runtime expansion evaluates precompiled expression IR and materializes final string.

## 8.7 Numeric/string formatting in interpolation

- Interpolation expression result is converted to string by URQL conversion rules.
- Numeric formatting uses invariant culture.
- Default numeric format: `G17` (assumption).
- `##NN$` char-code interpolation:
  - `NN` is treated as byte value `0..255`.
  - Byte is decoded using runtime context `CharCodeEncodingName` (typically derived from loaded quest file encoding).
  - This preserves DOS/FireURQ behavior for non-ASCII ranges while runtime stays Unicode internally.

## 9. Commands Semantics (MVP)

## 9.1 Assignment

- `x = expr` writes numeric/string/bool result into variable store.
- Variable names are case-insensitive.

## 9.2 `instr`

- `instr name = textExpr`
- Marks variable as string-preferred and assigns string value.
- Whitespace preservation assumption: preserve exactly as parsed post-comment-removal.

## 9.3 `if then else`

- Evaluate condition truthiness.
- Execute `then` chain or `else` chain.
- `&` chains execute left-to-right.

## 9.4 `goto`

- Resolve target label text (after substitution/interpolation).
- If label exists: jump instruction pointer to label entry.
- If missing: warning diagnostic, continue to next instruction.

## 9.5 `proc` / `end`

- `proc label`: push return address; jump to label.
- `end`:
  - If proc stack non-empty: pop and return.
  - Else: terminate run segment (location boundary halt).

This models classic URQL location/procedure behavior sufficiently for core tests.

## 9.6 `p` / `pln`

- `p text` appends text to output buffer without newline.
- `pln text` appends text then newline.
- Both commands resolve interpolation in argument text at runtime.

## 9.7 `btn`

- `btn target, caption` appends a pending button action.
- `target` is resolved through interpolation at runtime.
- `caption` is resolved through interpolation at runtime.
- Buttons are stored in insertion order for deterministic selection.

## 9.8 Inventory Commands

- `inv+ [count,] item`:
  - Adds `count` (default `1`) to inventory item.
  - Item keys are case-insensitive.
  - If resulting count is `<= 0`, item is removed.
- `inv- [count,] item`:
  - Removes `count` (default `1`) from inventory item.
  - If resulting count is `<= 0`, item is removed.
- `invkill`:
  - Without argument: clears entire inventory.
  - With item argument: removes only that item.
- `inv_<item>` bridge:
  - Read returns current numeric count.
  - Assignment writes count (`<= 0` removes item).

## 9.9 `perkill`

- `perkill` clears normal variable store.
- Inventory is not affected by `perkill` (matches DOS docs pairing `perkill` + `invkill`).

## 10. Runtime Choice Model

Button/stop behavior:

- On top-level `end`, if pending buttons exist, VM enters `WaitingForChoice`.
- Host selects action via `ChooseButton(id)`; VM jumps to action target.
- If no buttons exist at top-level `end`, VM halts normally.

State and host API:

- `Step()`
- `RunUntilWaitOrHalt(maxInstructions)`
- `ChooseButton(buttonId)`
- `RunUntilHalt(maxInstructions)`

## 11. Flow and Runtime Model

Program state:

- Instruction pointer
- Variable store
- Call stack (for `proc`)
- Output buffer
- Pending button list
- Diagnostics sink

One VM step:

- Fetch current IR instruction
- Execute instruction atomically
- Advance IP (or jump)

Run modes:

- `Step()`
- `RunUntilWaitOrHalt(maxInstructions)`
- `RunUntilHalt(maxInstructions)`

Halting:

- Explicit end-of-program
- Top-level `end`
- Fatal runtime error
- Instruction budget exceeded (returns non-fatal stop reason)

Dynamic statement execution path:

- VM may execute a dynamic statement produced by interpolation expansion.
- This path always uses the single-statement restricted parser/executor mode described in 8.5.

Inventory/use interaction:

- Bare identifier fallback in expressions:
  - If normal variable is missing and inventory item with same name exists, expression resolves to inventory count.
  - This enables DOS-style checks like `if not Веревка then ...`.
- `use_...` labels:
  - VM exposes host entrypoints for inventory actions:
    - `UseInventoryItem(item[, action])`
    - `InvokeUseLabel(labelName)` (for labels like `use_inv_Запись`)
  - Use-label execution is `proc`-like (returns to previous context on `end`).
  - When triggered while waiting at top-level `end`, VM returns back to `WaitingForChoice` after use-label return.

## 12. Diagnostics

Every diagnostic contains:

- Code (e.g., `URQL0012`)
- Severity (`Error`, `Warning`, `Info`)
- Message (English only)
- File/line/column span

Parser recovery:

- On statement parse failure, synchronize to next line.
- Continue collecting diagnostics.

Unsupported command policy:

- Parser option `AllowUnknownCommands` controls diagnostic severity for unsupported commands.
- `AllowUnknownCommands=true` (default/permissive): `UnknownCommand` warning + no-op statement.
- `AllowUnknownCommands=false` (strict): `UnknownCommand` error + no-op statement.
- `%...` macro-style lines (e.g. `%include ...`) follow the same policy and are currently treated as unsupported commands in core runtime.

## 13. IR Model (Abstract)

Instruction categories:

- `EvalExpr -> temp`
- `StoreVar`
- `Jump`
- `JumpIfFalse`
- `Call`
- `ReturnOrHalt`
- `Print`
- `PrintLn`
- `AddButton`
- `WaitForChoice`
- `ExpandTemplate`
- `ExecDynamicSingle`
- `NoOp` / `Diag`

IR is linear with label table (`label -> instruction index`).

## 14. Compatibility Notes

DOS_URQ-aligned:

- Case-insensitive language elements
- Label-first execution model
- `proc`/`goto` concept
- `&` command chaining
- `#...$` runtime interpolation behavior

FireURQ-aligned (expression behavior only):

- `<>` supported
- String arithmetic behavior
- Operator-local substitution model (no cross-statement code generation)

## 15. Assumptions (explicit defaults for missing/ambiguous parts)

1. Strings use double quotes with C-like escapes in new core.
2. Unicode is supported internally; `##NN$` maps by quest encoding:
   - UTF sources: `NN` is Unicode code point.
   - `cp1251/cp866/koi8-r` sources: `NN` is byte value decoded through that codepage.
3. Number->string coercion is empty string for compatibility, except explicit numeric formatting in `#<expr>$`.
4. Interpolation expansion capped at 16 passes to prevent infinite expansion.
5. `goto/proc` to unknown label is warning + no-op.
6. Duplicate labels bind to first declaration; later duplicates warn.
7. `end` at top-level halts execution.
8. `btn` target is resolved at runtime; unknown target chosen by user results in warning + no jump.
9. Command registry governs extensibility; unknown commands become parser diagnostics (warning in permissive mode, error in strict mode).
10. Dynamic single-statement execution whitelist includes assignment, `instr`, `p`, `pln`, `btn`, `goto`, `proc`, `end`, `perkill`, `invkill`, `inv+`, `inv-`.

## 16. Future Extensions (non-MVP)

- Input/pause/anykey, save/load.
- Include preprocessing (`%include` / `include`) with cycle-safe graph resolution.
- FireURQ decorators and scene model.
- `common` location semantics and `count_` behavior variants.
- `fp_prec`, `instr_leave_spc`, legacy exact formatting quirks.
