import {
  DiagnosticBag,
  type Diagnostic,
  type DiagnosticSeverity
} from "./diagnostics.js";
import { Lexer } from "./lexer.js";
import { sourcePosition, sourceSpan, type SourcePosition, type SourceSpan } from "./source.js";
import { tokenKind, type Token, type TokenKind } from "./token.js";
import type {
  AssignmentStatementSyntax,
  BinaryExpressionSyntax,
  BtnStatementSyntax,
  EndStatementSyntax,
  ExpressionSyntax,
  GotoStatementSyntax,
  IdentifierExpressionSyntax,
  IfStatementSyntax,
  InstrStatementSyntax,
  InvalidStatementSyntax,
  InvAddStatementSyntax,
  InvRemoveStatementSyntax,
  InvkillStatementSyntax,
  LabelSyntax,
  ParenthesizedExpressionSyntax,
  PerkillStatementSyntax,
  PrintStatementSyntax,
  ProcStatementSyntax,
  ProgramSyntax,
  RawTextExpressionSyntax,
  StatementLineSyntax,
  StatementSyntax,
  StringLiteralExpressionSyntax,
  UnaryExpressionSyntax,
  UnknownCommandStatementSyntax
} from "./ast.js";
import { defaultParserOptions, type ParserOptions } from "./parser-options.js";

export const parseDiagnosticCode = {
  expectedToken: "URQL2001",
  unexpectedToken: "URQL2002",
  invalidStatement: "URQL2003",
  unknownCommand: "URQL2004"
} as const;

export interface ParseResult {
  program: ProgramSyntax;
  diagnostics: Diagnostic[];
}

export interface ExpressionParseResult {
  expression: ExpressionSyntax;
  diagnostics: Diagnostic[];
}

export class Parser {
  private readonly tokens: Token[];
  private readonly options: Required<ParserOptions>;
  private readonly diagnostics = new DiagnosticBag();
  private readonly statementTerminators: Set<TokenKind>[] = [];
  private index = 0;

  private constructor(tokens: Token[], lexerDiagnostics: Diagnostic[], options?: ParserOptions) {
    this.tokens = tokens;
    this.options = { ...defaultParserOptions, ...options };
    for (const diagnostic of lexerDiagnostics) {
      this.diagnostics.items.push(diagnostic);
    }
  }

  static parse(source: string, options?: ParserOptions): ParseResult {
    const lex = Lexer.lex(source);
    const parser = new Parser(lex.tokens, lex.diagnostics, options);
    const program = parser.parseProgram();
    return {
      program,
      diagnostics: parser.diagnostics.items
    };
  }

  static parseExpressionText(expressionText: string): ExpressionParseResult {
    const lex = Lexer.lex(expressionText);
    const parser = new Parser(lex.tokens, lex.diagnostics);
    const expression = parser.parseExpression();

    while (parser.currentKind === tokenKind.newLine) {
      parser.nextToken();
    }

    if (parser.currentKind !== tokenKind.endOfFile) {
      parser.reportUnexpectedToken(
        `Unexpected trailing token '${parser.current.text}' in expression.`,
        parser.current.span
      );
    }

    return {
      expression,
      diagnostics: parser.diagnostics.items
    };
  }

  private get current(): Token {
    return this.peek(0);
  }

  private get currentKind(): TokenKind {
    return this.current.kind as TokenKind;
  }

  private peek(offset: number): Token {
    let nextIndex = this.index + offset;
    if (nextIndex < 0) {
      nextIndex = 0;
    }

    return this.tokens[nextIndex] ?? this.tokens[this.tokens.length - 1]!;
  }

  private nextToken(): Token {
    const current = this.current;
    if (this.index < this.tokens.length - 1) {
      this.index += 1;
    }

    return current;
  }

  private match(kind: TokenKind): boolean {
    if (this.current.kind !== kind) {
      return false;
    }

    this.nextToken();
    return true;
  }

  private parseProgram(): ProgramSyntax {
    const lines: StatementLineSyntax[] = [];

    while (true) {
      while (this.match(tokenKind.newLine)) {
        // skip empty lines
      }

      if (this.currentKind === tokenKind.endOfFile) {
        break;
      }

      lines.push(this.parseLine());
    }

    const span =
      lines.length === 0
        ? sourceSpan(sourcePosition(1, 1), sourcePosition(1, 1))
        : sourceSpan(lines[0]!.span.start, lines[lines.length - 1]!.span.end);

    return {
      kind: "Program",
      lines,
      span
    };
  }

  private parseLine(): StatementLineSyntax {
    const lineStart = this.current.span.start;
    let label: LabelSyntax | null = null;
    let labelConsumesLine = false;

    if (this.match(tokenKind.colon)) {
      const parsed = this.parseLabelDeclaration(lineStart);
      label = parsed.label;
      labelConsumesLine = parsed.consumesLine;
    }

    const statements: StatementSyntax[] = [];
    const lineKind = this.currentKind;
    if (!labelConsumesLine && lineKind !== tokenKind.newLine && lineKind !== tokenKind.endOfFile) {
      statements.push(this.parseStatement());

      while (this.match(tokenKind.ampersand)) {
        const nextKind = this.currentKind;
        if (nextKind === tokenKind.newLine || nextKind === tokenKind.endOfFile) {
          this.reportExpectedToken("Expected statement after '&'.", this.previousTokenSpan());
          break;
        }

        statements.push(this.parseStatement());
      }
    }

    if (this.current.kind !== tokenKind.newLine && this.current.kind !== tokenKind.endOfFile) {
      this.reportUnexpectedToken(
        `Unexpected token '${this.current.text}' at end of line.`,
        this.current.span
      );
      this.syncToLineEnd();
    }

    this.match(tokenKind.newLine);

    const lineEnd =
      statements.length > 0
        ? statements[statements.length - 1]!.span.end
        : label?.span.end ?? lineStart;

    return {
      kind: "StatementLine",
      label,
      statements,
      span: sourceSpan(lineStart, lineEnd)
    };
  }

  private parseLabelDeclaration(lineStart: SourcePosition): {
    label: LabelSyntax | null;
    consumesLine: boolean;
  } {
    if (this.current.kind === tokenKind.newLine || this.current.kind === tokenKind.endOfFile) {
      this.reportExpectedToken(
        "Expected label identifier after ':'.",
        sourceSpan(lineStart, lineStart)
      );
      return { label: null, consumesLine: false };
    }

    const parts: string[] = [];
    const start = this.current.span.start;
    let end = start;

    while (true) {
      const currentKind = this.currentKind;
      if (currentKind === tokenKind.newLine || currentKind === tokenKind.endOfFile) {
        break;
      }

      const token = this.nextToken();
      parts.push(token.text);
      end = token.span.end;
    }

    const name = reconstructRawText(parts).trim();
    if (name.length === 0) {
      this.reportExpectedToken("Expected label identifier after ':'.", sourceSpan(start, end));
      return { label: null, consumesLine: false };
    }

    return {
      label: {
        kind: "Label",
        name,
        span: sourceSpan(start, end)
      },
      consumesLine: true
    };
  }

  private parseStatement(): StatementSyntax {
    const start = this.current.span.start;

    if (
      this.current.kind === tokenKind.identifier &&
      this.peek(1).kind !== tokenKind.equals &&
      this.hasEqualsBeforeTerminator()
    ) {
      return this.parseAssignmentStatement();
    }

    if (this.current.kind === tokenKind.keywordInstr) {
      return this.parseInstrStatement();
    }

    if (this.current.kind === tokenKind.keywordIf) {
      return this.parseIfStatement();
    }

    if (this.current.kind === tokenKind.keywordGoto) {
      return this.parseGotoStatement();
    }

    if (this.current.kind === tokenKind.keywordProc) {
      return this.parseProcStatement();
    }

    if (this.current.kind === tokenKind.keywordEnd) {
      return this.parseEndStatement();
    }

    if (
      this.current.kind === tokenKind.keywordP ||
      this.current.kind === tokenKind.keywordPrint
    ) {
      return this.parsePrintStatement(false);
    }

    if (
      this.current.kind === tokenKind.keywordPln ||
      this.current.kind === tokenKind.keywordPrintln
    ) {
      return this.parsePrintStatement(true);
    }

    if (this.current.kind === tokenKind.keywordBtn) {
      return this.parseBtnStatement();
    }

    if (this.current.kind === tokenKind.identifier && isWord(this.current, "perkill")) {
      return this.parsePerkillStatement();
    }

    if (this.current.kind === tokenKind.identifier && isWord(this.current, "invkill")) {
      return this.parseInvkillStatement();
    }

    if (
      this.current.kind === tokenKind.identifier &&
      isWord(this.current, "inv") &&
      this.peek(1).kind === tokenKind.plus
    ) {
      return this.parseInvDeltaStatement(true);
    }

    if (
      this.current.kind === tokenKind.identifier &&
      isWord(this.current, "inv") &&
      this.peek(1).kind === tokenKind.minus
    ) {
      return this.parseInvDeltaStatement(false);
    }

    if (this.current.kind === tokenKind.identifier && this.peek(1).kind === tokenKind.equals) {
      return this.parseAssignmentStatement();
    }

    if (this.current.kind === tokenKind.percent) {
      return this.parsePercentMacroAsUnknown();
    }

    return this.parseUnknownOrInvalid(start);
  }

  private parseAssignmentStatement(): AssignmentStatementSyntax {
    const nameStart = this.current.span.start;
    const name = this.parseVariableNameUntilEquals("Expected '=' in assignment.");
    const expression = this.parseExpression();
    return {
      kind: "AssignmentStatement",
      name,
      expression,
      span: sourceSpan(nameStart, expression.span.end)
    };
  }

  private parseInstrStatement(): InstrStatementSyntax {
    const start = this.nextToken();
    const name = this.parseVariableNameUntilEquals("Expected '=' in instr assignment.");
    const expression = this.parseExpression();
    return {
      kind: "InstrStatement",
      name,
      expression,
      span: sourceSpan(start.span.start, expression.span.end)
    };
  }

  private parseGotoStatement(): GotoStatementSyntax {
    const start = this.nextToken();
    const target = this.parseExpression();
    return {
      kind: "GotoStatement",
      target,
      span: sourceSpan(start.span.start, target.span.end)
    };
  }

  private parseProcStatement(): ProcStatementSyntax {
    const start = this.nextToken();
    const target = this.parseExpression();
    return {
      kind: "ProcStatement",
      target,
      span: sourceSpan(start.span.start, target.span.end)
    };
  }

  private parseEndStatement(): EndStatementSyntax {
    const token = this.nextToken();
    return {
      kind: "EndStatement",
      span: token.span
    };
  }

  private parsePrintStatement(appendNewLine: boolean): PrintStatementSyntax {
    const start = this.nextToken();
    const textExpression = this.parseRawTextExpression();
    return {
      kind: "PrintStatement",
      textExpression,
      appendNewLine,
      span: sourceSpan(start.span.start, textExpression.span.end)
    };
  }

  private parseBtnStatement(): BtnStatementSyntax {
    const start = this.nextToken();
    const targetExpression = this.parseRawTextExpression(tokenKind.comma);
    this.expect(tokenKind.comma, "Expected ',' after btn target.");
    const captionExpression = this.parseRawTextExpression();
    return {
      kind: "BtnStatement",
      targetExpression,
      captionExpression,
      span: sourceSpan(start.span.start, captionExpression.span.end)
    };
  }

  private parsePerkillStatement(): PerkillStatementSyntax {
    const token = this.nextToken();
    return {
      kind: "PerkillStatement",
      span: token.span
    };
  }

  private parseInvkillStatement(): InvkillStatementSyntax {
    const start = this.nextToken();
    if (
      this.current.kind === tokenKind.newLine ||
      this.current.kind === tokenKind.endOfFile ||
      this.current.kind === tokenKind.ampersand
    ) {
      return {
        kind: "InvkillStatement",
        itemExpression: null,
        span: start.span
      };
    }

    const itemExpression = this.parseRawTextExpression();
    return {
      kind: "InvkillStatement",
      itemExpression,
      span: sourceSpan(start.span.start, itemExpression.span.end)
    };
  }

  private parseInvDeltaStatement(add: boolean): StatementSyntax {
    const invToken = this.nextToken();
    const opToken = this.nextToken();

    if (
      this.current.kind === tokenKind.newLine ||
      this.current.kind === tokenKind.endOfFile ||
      this.current.kind === tokenKind.ampersand
    ) {
      this.reportExpectedToken("Expected inventory item after inv+ or inv-.", opToken.span);
      return {
        kind: "InvalidStatement",
        reason: "Invalid inventory command.",
        span: sourceSpan(invToken.span.start, opToken.span.end)
      };
    }

    const counted = this.tryReadCountAndComma();
    if (counted) {
      const itemExpression = this.parseRawTextExpression();
      if (itemExpression.kind === "RawTextExpression" && itemExpression.rawText.trim().length === 0) {
        this.reportExpectedToken(
          "Expected inventory item after comma in inv+ or inv-.",
          opToken.span
        );
        return {
          kind: "InvalidStatement",
          reason: "Invalid inventory command.",
          span: sourceSpan(invToken.span.start, itemExpression.span.end)
        };
      }

      return add
        ? {
            kind: "InvAddStatement",
            countExpression: counted,
            itemExpression,
            span: sourceSpan(invToken.span.start, itemExpression.span.end)
          }
        : {
            kind: "InvRemoveStatement",
            countExpression: counted,
            itemExpression,
            span: sourceSpan(invToken.span.start, itemExpression.span.end)
          };
    }

    const itemExpression = this.parseRawTextExpression();
    return add
      ? {
          kind: "InvAddStatement",
          countExpression: null,
          itemExpression,
          span: sourceSpan(invToken.span.start, itemExpression.span.end)
        }
      : {
          kind: "InvRemoveStatement",
          countExpression: null,
          itemExpression,
          span: sourceSpan(invToken.span.start, itemExpression.span.end)
        };
  }

  private parseIfStatement(): IfStatementSyntax {
    const ifToken = this.nextToken();
    const condition = this.parseExpression();
    this.expect(tokenKind.keywordThen, "Expected 'then' in if statement.");

    const thenStatements = this.parseInlineChainUntil(
      tokenKind.keywordElse,
      tokenKind.newLine,
      tokenKind.endOfFile
    );
    let elseStatements: StatementSyntax[] | null = null;

    if (this.match(tokenKind.keywordElse)) {
      elseStatements = this.parseInlineChainUntil(tokenKind.newLine, tokenKind.endOfFile);
    }

    const end =
      elseStatements && elseStatements.length > 0
        ? elseStatements[elseStatements.length - 1]!.span.end
        : thenStatements.length > 0
          ? thenStatements[thenStatements.length - 1]!.span.end
          : condition.span.end;

    return {
      kind: "IfStatement",
      condition,
      thenStatements,
      elseStatements,
      span: sourceSpan(ifToken.span.start, end)
    };
  }

  private parseInlineChainUntil(...terminators: TokenKind[]): StatementSyntax[] {
    this.statementTerminators.push(new Set(terminators));

    try {
      const result: StatementSyntax[] = [];
      if (terminators.includes(this.current.kind)) {
        return result;
      }

      result.push(this.parseStatement());
      while (this.match(tokenKind.ampersand)) {
        if (terminators.includes(this.current.kind)) {
          this.reportExpectedToken("Expected statement after '&'.", this.previousTokenSpan());
          break;
        }

        result.push(this.parseStatement());
      }

      return result;
    } finally {
      this.statementTerminators.pop();
    }
  }

  private parseUnknownOrInvalid(start: SourcePosition): StatementSyntax {
    if (this.isCommandLike(this.current.kind)) {
      const severity: DiagnosticSeverity = this.options.allowUnknownCommands ? "warning" : "error";
      return this.parseUnknownCommandStatement(severity);
    }

    return this.parseInvalidStatement(start);
  }

  private parseInvalidStatement(start: SourcePosition): InvalidStatementSyntax {
    const token = this.nextToken();
    this.report(
      parseDiagnosticCode.invalidStatement,
      "error",
      `Invalid statement start token '${token.text}'.`,
      token.span
    );

    return {
      kind: "InvalidStatement",
      reason: "Invalid statement.",
      span: sourceSpan(start, token.span.end)
    };
  }

  private parseUnknownCommandStatement(severity: DiagnosticSeverity): UnknownCommandStatementSyntax {
    const command = this.nextToken();
    const argumentsExpression = this.parseRawTextExpression();
    this.report(
      parseDiagnosticCode.unknownCommand,
      severity,
      `Unknown command '${command.text}' treated as no-op in compatibility mode.`,
      command.span
    );

    return {
      kind: "UnknownCommandStatement",
      commandName: command.text,
      argumentsExpression,
      span: sourceSpan(command.span.start, argumentsExpression.span.end)
    };
  }

  private parsePercentMacroAsUnknown(): UnknownCommandStatementSyntax {
    const percent = this.nextToken();
    let commandName = percent.text;
    let commandEnd = percent.span.end;

    if (this.current.kind === tokenKind.identifier) {
      const name = this.nextToken();
      commandName += name.text;
      commandEnd = name.span.end;
    }

    const argumentsExpression = this.parseRawTextExpression();
    const severity: DiagnosticSeverity = this.options.allowUnknownCommands ? "warning" : "error";
    this.report(
      parseDiagnosticCode.unknownCommand,
      severity,
      `Unknown command '${commandName}' treated as no-op in compatibility mode.`,
      sourceSpan(percent.span.start, commandEnd)
    );

    return {
      kind: "UnknownCommandStatement",
      commandName,
      argumentsExpression,
      span: sourceSpan(percent.span.start, argumentsExpression.span.end)
    };
  }

  private expect(kind: TokenKind, message: string): Token {
    if (this.current.kind === kind) {
      return this.nextToken();
    }

    this.reportExpectedToken(message, this.current.span);
    return {
      kind,
      text: "",
      span: this.current.span
    };
  }

  private previousTokenSpan(): SourceSpan {
    const previousIndex = Math.max(0, this.index - 1);
    return this.tokens[previousIndex]!.span;
  }

  private syncToLineEnd(): void {
    while (this.current.kind !== tokenKind.newLine && this.current.kind !== tokenKind.endOfFile) {
      this.nextToken();
    }
  }

  private parseExpression(parentPrecedence = 0): ExpressionSyntax {
    const unaryPrecedence = getUnaryPrecedence(this.current.kind);
    let left: ExpressionSyntax;

    if (unaryPrecedence > 0 && unaryPrecedence >= parentPrecedence) {
      const operator = this.nextToken();
      const operand = this.parseExpression(unaryPrecedence);
      left = {
        kind: "UnaryExpression",
        operator: operator.kind,
        operand,
        span: sourceSpan(operator.span.start, operand.span.end)
      } satisfies UnaryExpressionSyntax;
    } else {
      left = this.parsePrimary();
    }

    while (true) {
      const precedence = getBinaryPrecedence(this.current.kind);
      if (precedence === 0 || precedence <= parentPrecedence) {
        break;
      }

      const operator = this.nextToken();
      const right = this.parseExpression(precedence);
      left = {
        kind: "BinaryExpression",
        left,
        operator: operator.kind,
        right,
        span: sourceSpan(left.span.start, right.span.end)
      } satisfies BinaryExpressionSyntax;
    }

    return left;
  }

  private parsePrimary(): ExpressionSyntax {
    const token = this.current;

    if (token.kind === tokenKind.number) {
      this.nextToken();
      return {
        kind: "NumberLiteralExpression",
        value: typeof token.value === "number" ? token.value : 0,
        rawText: token.text,
        span: token.span
      };
    }

    if (token.kind === tokenKind.string) {
      this.nextToken();
      return {
        kind: "StringLiteralExpression",
        value: typeof token.value === "string" ? token.value : "",
        rawText: token.text,
        span: token.span
      } satisfies StringLiteralExpressionSyntax;
    }

    if (token.kind === tokenKind.identifier) {
      return this.parseIdentifierLikeExpression();
    }

    if (token.kind === tokenKind.openParen) {
      const open = this.nextToken();
      const inner = this.parseExpression();
      const close = this.expect(tokenKind.closeParen, "Expected ')' to close expression.");
      return {
        kind: "ParenthesizedExpression",
        inner,
        span: sourceSpan(open.span.start, close.span.end)
      } satisfies ParenthesizedExpressionSyntax;
    }

    if (isIdentifierLikeInExpression(token.kind)) {
      return this.parseIdentifierLikeExpression();
    }

    this.reportUnexpectedToken(`Expected expression, found '${token.text}'.`, token.span);
    this.nextToken();
    return {
      kind: "IdentifierExpression",
      name: "",
      span: token.span
    } satisfies IdentifierExpressionSyntax;
  }

  private parseVariableNameUntilEquals(expectedEqualsMessage: string): string {
    const parts: string[] = [];

    while (
      this.current.kind !== tokenKind.equals &&
      this.current.kind !== tokenKind.newLine &&
      this.current.kind !== tokenKind.endOfFile &&
      this.current.kind !== tokenKind.ampersand
    ) {
      if (this.statementTerminators.length > 0) {
        const terminators = this.statementTerminators[this.statementTerminators.length - 1]!;
        if (terminators.has(this.current.kind)) {
          break;
        }
      }

      parts.push(this.nextToken().text);
    }

    this.expect(tokenKind.equals, expectedEqualsMessage);
    const name = reconstructRawText(parts).trim();
    if (name.length === 0) {
      this.reportExpectedToken(
        "Expected variable name in assignment.",
        this.previousTokenSpan()
      );
      return "";
    }

    return name;
  }

  private parseIdentifierLikeExpression(): IdentifierExpressionSyntax {
    const start = this.current.span.start;
    const parts: string[] = [];
    let end = start;

    while (isIdentifierLikeInExpression(this.current.kind)) {
      const token = this.nextToken();
      parts.push(token.text);
      end = token.span.end;
    }

    return {
      kind: "IdentifierExpression",
      name: reconstructRawText(parts).trim(),
      span: sourceSpan(start, end)
    };
  }

  private tryReadCountAndComma(): ExpressionSyntax | null {
    const savedIndex = this.index;
    const savedDiagnosticsCount = this.diagnostics.items.length;
    const countExpression = this.parseExpression();

    if (this.current.kind === tokenKind.comma) {
      this.nextToken();
      return countExpression;
    }

    this.index = savedIndex;
    this.diagnostics.items.length = savedDiagnosticsCount;
    return null;
  }

  private parseRawTextExpression(until?: TokenKind): RawTextExpressionSyntax {
    const parts: string[] = [];
    const start = this.current.span.start;
    let end = start;

    while (
      this.current.kind !== tokenKind.newLine &&
      this.current.kind !== tokenKind.endOfFile &&
      this.current.kind !== tokenKind.ampersand &&
      this.current.kind !== until
    ) {
      if (this.statementTerminators.length > 0) {
        const terminators = this.statementTerminators[this.statementTerminators.length - 1]!;
        if (terminators.has(this.current.kind)) {
          break;
        }
      }

      const token = this.nextToken();
      parts.push(token.text);
      end = token.span.end;
    }

    const rawText = reconstructRawText(parts).trim();
    return {
      kind: "RawTextExpression",
      rawText,
      span: sourceSpan(start, end)
    };
  }

  private hasEqualsBeforeTerminator(): boolean {
    for (let index = this.index; index < this.tokens.length; index += 1) {
      const kind = this.tokens[index]!.kind;
      if (kind === tokenKind.newLine || kind === tokenKind.endOfFile || kind === tokenKind.ampersand) {
        return false;
      }

      if (this.statementTerminators.length > 0) {
        const terminators = this.statementTerminators[this.statementTerminators.length - 1]!;
        if (terminators.has(kind)) {
          return false;
        }
      }

      if (kind === tokenKind.equals) {
        return true;
      }
    }

    return false;
  }

  private isCommandLike(kind: TokenKind): boolean {
    return (
      kind === tokenKind.identifier ||
      kind === tokenKind.keywordIf ||
      kind === tokenKind.keywordThen ||
      kind === tokenKind.keywordElse ||
      kind === tokenKind.keywordGoto ||
      kind === tokenKind.keywordProc ||
      kind === tokenKind.keywordEnd ||
      kind === tokenKind.keywordInstr ||
      kind === tokenKind.keywordP ||
      kind === tokenKind.keywordPrint ||
      kind === tokenKind.keywordPln ||
      kind === tokenKind.keywordPrintln ||
      kind === tokenKind.keywordBtn
    );
  }

  private reportExpectedToken(message: string, span: SourceSpan): void {
    this.report(parseDiagnosticCode.expectedToken, "error", message, span);
  }

  private reportUnexpectedToken(message: string, span: SourceSpan): void {
    this.report(parseDiagnosticCode.unexpectedToken, "error", message, span);
  }

  private report(
    code: string,
    severity: DiagnosticSeverity,
    message: string,
    span: SourceSpan
  ): void {
    this.diagnostics.items.push({
      code,
      severity,
      message,
      span
    } as Diagnostic);
  }
}

function getUnaryPrecedence(kind: TokenKind): number {
  switch (kind) {
    case tokenKind.keywordNot:
    case tokenKind.plus:
    case tokenKind.minus:
      return 7;
    default:
      return 0;
  }
}

function getBinaryPrecedence(kind: TokenKind): number {
  switch (kind) {
    case tokenKind.star:
    case tokenKind.slash:
    case tokenKind.percent:
      return 6;
    case tokenKind.plus:
    case tokenKind.minus:
      return 5;
    case tokenKind.equals:
    case tokenKind.notEquals:
    case tokenKind.doubleEquals:
    case tokenKind.less:
    case tokenKind.greater:
    case tokenKind.lessOrEquals:
    case tokenKind.greaterOrEquals:
      return 4;
    case tokenKind.keywordAnd:
      return 3;
    case tokenKind.keywordOr:
      return 2;
    default:
      return 0;
  }
}

function isIdentifierLikeInExpression(kind: TokenKind): boolean {
  return kind === tokenKind.identifier;
}

function isWord(token: Token, word: string): boolean {
  return token.text.localeCompare(word, undefined, { sensitivity: "accent" }) === 0;
}

function reconstructRawText(parts: string[]): string {
  let result = "";

  for (const part of parts) {
    if (part.length === 0) {
      continue;
    }

    if (result.length === 0) {
      result = part;
      continue;
    }

    if (needsSpaceBetween(result[result.length - 1]!, part[0]!)) {
      result += " ";
    }

    result += part;
  }

  return result;
}

function needsSpaceBetween(left: string, right: string): boolean {
  if (right === "," || right === "." || right === "!" || right === "?" || right === ":" || right === ";") {
    return false;
  }

  if (left === "(" || left === "[" || left === "{") {
    return false;
  }

  if (left === "," || left === "." || left === "!" || left === "?" || left === ":" || left === ";") {
    return true;
  }

  const leftWord = /[\p{L}\p{N}_]/u.test(left);
  const rightWord = /[\p{L}\p{N}_]/u.test(right);
  return leftWord && rightWord;
}
