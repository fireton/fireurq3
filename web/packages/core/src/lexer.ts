import { DiagnosticBag, diagnosticCode } from "./diagnostics.js";
import {
  sourcePosition,
  sourceSpan,
  type SourcePosition,
  type SourceSpan
} from "./source.js";
import { tokenKind, type LexResult, type Token, type TokenKind } from "./token.js";

interface PhysicalLine {
  text: string;
  lineNumber: number;
}

interface NormalizedSource {
  text: string;
  positionMap: SourcePosition[];
  endPosition: SourcePosition;
}

const keywordMap = new Map<string, TokenKind>([
  ["if", tokenKind.keywordIf],
  ["then", tokenKind.keywordThen],
  ["else", tokenKind.keywordElse],
  ["goto", tokenKind.keywordGoto],
  ["proc", tokenKind.keywordProc],
  ["end", tokenKind.keywordEnd],
  ["instr", tokenKind.keywordInstr],
  ["p", tokenKind.keywordP],
  ["print", tokenKind.keywordPrint],
  ["pln", tokenKind.keywordPln],
  ["println", tokenKind.keywordPrintln],
  ["btn", tokenKind.keywordBtn],
  ["and", tokenKind.keywordAnd],
  ["or", tokenKind.keywordOr],
  ["not", tokenKind.keywordNot]
]);

export class Lexer {
  private readonly normalized: NormalizedSource;
  private readonly diagnostics = new DiagnosticBag();
  private readonly tokens: Token[] = [];
  private index = 0;

  private constructor(sourceText: string) {
    this.normalized = normalizeSource(sourceText);
  }

  static lex(sourceText: string): LexResult {
    const lexer = new Lexer(sourceText);
    lexer.tokenize();
    return {
      tokens: lexer.tokens,
      diagnostics: lexer.diagnostics.items
    };
  }

  private get current(): string {
    return this.normalized.text[this.index] ?? "\0";
  }

  private peek(offset = 1): string {
    return this.normalized.text[this.index + offset] ?? "\0";
  }

  private positionAt(index: number): SourcePosition {
    if (this.normalized.positionMap.length === 0) {
      return sourcePosition(1, 1);
    }

    if (index >= 0 && index < this.normalized.positionMap.length) {
      return this.normalized.positionMap[index]!;
    }

    return this.normalized.endPosition;
  }

  private spanFromIndices(startIndex: number, endExclusive: number): SourceSpan {
    const start = this.positionAt(startIndex);
    const end = this.positionAt(Math.max(startIndex, endExclusive - 1));
    return sourceSpan(start, end);
  }

  private tokenize(): void {
    while (this.index < this.normalized.text.length) {
      if (this.trySkipWhitespaceAndComments()) {
        continue;
      }

      const start = this.index;
      const current = this.current;

      switch (current) {
        case "\n":
          this.index += 1;
          this.addToken(tokenKind.newLine, "\n", start, this.index);
          continue;
        case ":":
          this.index += 1;
          this.addToken(tokenKind.colon, ":", start, this.index);
          continue;
        case ",":
          this.index += 1;
          this.addToken(tokenKind.comma, ",", start, this.index);
          continue;
        case "&":
          this.index += 1;
          this.addToken(tokenKind.ampersand, "&", start, this.index);
          continue;
        case "(":
          this.index += 1;
          this.addToken(tokenKind.openParen, "(", start, this.index);
          continue;
        case ")":
          this.index += 1;
          this.addToken(tokenKind.closeParen, ")", start, this.index);
          continue;
        case "+":
          this.index += 1;
          this.addToken(tokenKind.plus, "+", start, this.index);
          continue;
        case "-":
          this.index += 1;
          this.addToken(tokenKind.minus, "-", start, this.index);
          continue;
        case "*":
          this.index += 1;
          this.addToken(tokenKind.star, "*", start, this.index);
          continue;
        case "/":
          this.index += 1;
          this.addToken(tokenKind.slash, "/", start, this.index);
          continue;
        case "%":
          this.index += 1;
          this.addToken(tokenKind.percent, "%", start, this.index);
          continue;
        case "#":
          this.index += 1;
          this.addToken(tokenKind.hash, "#", start, this.index);
          continue;
        case "$":
          this.index += 1;
          this.addToken(tokenKind.dollar, "$", start, this.index);
          continue;
        case "?":
          this.index += 1;
          this.addToken(tokenKind.question, "?", start, this.index);
          continue;
        case ".":
          this.index += 1;
          this.addToken(tokenKind.dot, ".", start, this.index);
          continue;
        case "!":
          this.index += 1;
          this.addToken(tokenKind.exclamation, "!", start, this.index);
          continue;
        case "=":
          if (this.peek() === "=") {
            this.index += 2;
            this.addToken(tokenKind.doubleEquals, "==", start, this.index);
          } else {
            this.index += 1;
            this.addToken(tokenKind.equals, "=", start, this.index);
          }
          continue;
        case "<":
          if (this.peek() === "=") {
            this.index += 2;
            this.addToken(tokenKind.lessOrEquals, "<=", start, this.index);
          } else if (this.peek() === ">") {
            this.index += 2;
            this.addToken(tokenKind.notEquals, "<>", start, this.index);
          } else {
            this.index += 1;
            this.addToken(tokenKind.less, "<", start, this.index);
          }
          continue;
        case ">":
          if (this.peek() === "=") {
            this.index += 2;
            this.addToken(tokenKind.greaterOrEquals, ">=", start, this.index);
          } else {
            this.index += 1;
            this.addToken(tokenKind.greater, ">", start, this.index);
          }
          continue;
        case "\"":
          this.readStringToken();
          continue;
        default:
          if (isIdentifierStart(current)) {
            this.readIdentifierToken();
            continue;
          }

          if (isAsciiDigit(current)) {
            this.readNumberToken();
            continue;
          }

          this.index += 1;
          this.diagnostics.reportError(
            diagnosticCode.unexpectedCharacter,
            `Unexpected character '${current}'.`,
            this.spanFromIndices(start, this.index)
          );
      }
    }

    this.addToken(
      tokenKind.endOfFile,
      "",
      this.normalized.text.length,
      this.normalized.text.length
    );
  }

  private trySkipWhitespaceAndComments(): boolean {
    let progressed = false;

    while (this.index < this.normalized.text.length) {
      const current = this.current;

      if (current === " " || current === "\t" || current === "\r" || current === "\f") {
        this.index += 1;
        progressed = true;
        continue;
      }

      if (current === ";") {
        progressed = true;
        while (this.index < this.normalized.text.length && this.current !== "\n") {
          this.index += 1;
        }

        continue;
      }

      if (current === "/" && this.peek() === "*") {
        progressed = true;
        const start = this.index;
        this.index += 2;

        while (
          this.index < this.normalized.text.length &&
          !(this.current === "*" && this.peek() === "/")
        ) {
          this.index += 1;
        }

        if (this.index >= this.normalized.text.length) {
          this.diagnostics.reportError(
            diagnosticCode.unterminatedBlockComment,
            "Unterminated block comment.",
            this.spanFromIndices(start, this.normalized.text.length)
          );
          return true;
        }

        this.index += 2;
        continue;
      }

      break;
    }

    return progressed;
  }

  private readIdentifierToken(): void {
    const start = this.index;
    this.index += 1;

    while (isIdentifierPart(this.current)) {
      this.index += 1;
    }

    const text = this.normalized.text.slice(start, this.index);
    const kind = keywordMap.get(text.toLowerCase()) ?? tokenKind.identifier;
    this.addToken(kind, text, start, this.index);
  }

  private readNumberToken(): void {
    const start = this.index;
    let hasDot = false;

    if (this.current === "0" && (this.peek() === "x" || this.peek() === "X")) {
      this.index += 2;
      while (isHexDigit(this.current)) {
        this.index += 1;
      }

      const hexText = this.normalized.text.slice(start, this.index);
      const digits = hexText.slice(2);
      if (digits.length === 0 || !/^[0-9a-fA-F]+$/.test(digits)) {
        this.diagnostics.reportError(
          diagnosticCode.invalidNumberLiteral,
          `Invalid number literal '${hexText}'.`,
          this.spanFromIndices(start, this.index)
        );
        this.addToken(tokenKind.number, hexText, start, this.index, 0);
        return;
      }

      this.addToken(tokenKind.number, hexText, start, this.index, Number.parseInt(digits, 16));
      return;
    }

    while (isAsciiDigit(this.current) || this.current === ".") {
      if (this.current === ".") {
        if (hasDot) {
          break;
        }

        hasDot = true;
      }

      this.index += 1;
    }

    const text = this.normalized.text.slice(start, this.index);
    const value = Number(text);
    if (Number.isNaN(value)) {
      this.diagnostics.reportError(
        diagnosticCode.invalidNumberLiteral,
        `Invalid number literal '${text}'.`,
        this.spanFromIndices(start, this.index)
      );
      this.addToken(tokenKind.number, text, start, this.index, 0);
      return;
    }

    this.addToken(tokenKind.number, text, start, this.index, value);
  }

  private readStringToken(): void {
    const start = this.index;
    this.index += 1;
    let value = "";
    let terminated = false;

    while (this.index < this.normalized.text.length) {
      const current = this.current;
      if (current === "\"") {
        this.index += 1;
        terminated = true;
        break;
      }

      if (current === "\\") {
        const escapeStart = this.index;
        this.index += 1;
        const escaped = this.current;

        if (escaped === "\0") {
          break;
        }

        switch (escaped) {
          case "\\":
            value += "\\";
            this.index += 1;
            break;
          case "\"":
            value += "\"";
            this.index += 1;
            break;
          case "n":
            value += "\n";
            this.index += 1;
            break;
          case "r":
            value += "\r";
            this.index += 1;
            break;
          case "t":
            value += "\t";
            this.index += 1;
            break;
          case "x": {
            this.index += 1;
            const h1 = this.current;
            this.index += 1;
            const h2 = this.current;

            if (!isHexDigit(h1) || !isHexDigit(h2)) {
              this.diagnostics.reportError(
                diagnosticCode.invalidEscapeSequence,
                "Invalid hex escape sequence. Expected \\xNN.",
                this.spanFromIndices(escapeStart, this.index + 1)
              );
              break;
            }

            this.index += 1;
            value += String.fromCharCode(Number.parseInt(`${h1}${h2}`, 16));
            break;
          }
          default:
            this.diagnostics.reportError(
              diagnosticCode.invalidEscapeSequence,
              `Invalid escape sequence '\\${escaped}'.`,
              this.spanFromIndices(escapeStart, this.index + 1)
            );
            value += escaped;
            this.index += 1;
            break;
        }

        continue;
      }

      if (current === "\n") {
        break;
      }

      value += current;
      this.index += 1;
    }

    if (!terminated) {
      this.diagnostics.reportError(
        diagnosticCode.unterminatedString,
        "Unterminated string literal.",
        this.spanFromIndices(start, this.index)
      );
    }

    const text = this.normalized.text.slice(start, Math.min(this.index, this.normalized.text.length));
    this.addToken(tokenKind.string, text, start, this.index, value);
  }

  private addToken(
    kind: TokenKind,
    text: string,
    start: number,
    endExclusive: number,
    value?: number | string
  ): void {
    this.tokens.push({
      kind,
      text,
      span: this.spanFromIndices(start, endExclusive),
      value
    });
  }
}

function normalizeSource(sourceText: string): NormalizedSource {
  const lines = splitPhysicalLines(sourceText);
  const output: string[] = [];
  const positions: SourcePosition[] = [];
  let hasPreviousLogicalLine = false;

  for (const line of lines) {
    const firstNonWhitespace = findFirstNonWhitespace(line.text);
    const isContinuation =
      hasPreviousLogicalLine &&
      firstNonWhitespace >= 0 &&
      line.text[firstNonWhitespace] === "_";

    if (isContinuation) {
      output.push(" ");
      positions.push(sourcePosition(line.lineNumber, firstNonWhitespace + 1));

      const offset = firstNonWhitespace + 1;
      for (let index = offset; index < line.text.length; index += 1) {
        output.push(line.text[index]!);
        positions.push(sourcePosition(line.lineNumber, index + 1));
      }
    } else {
      if (hasPreviousLogicalLine) {
        output.push("\n");
        positions.push(sourcePosition(line.lineNumber, 1));
      }

      for (let index = 0; index < line.text.length; index += 1) {
        output.push(line.text[index]!);
        positions.push(sourcePosition(line.lineNumber, index + 1));
      }
    }

    hasPreviousLogicalLine = true;
  }

  const endPosition =
    positions.length === 0
      ? sourcePosition(1, 1)
      : sourcePosition(
          positions[positions.length - 1]!.line,
          positions[positions.length - 1]!.column + 1
        );

  return {
    text: output.join(""),
    positionMap: positions,
    endPosition
  };
}

function findFirstNonWhitespace(text: string): number {
  for (let index = 0; index < text.length; index += 1) {
    if (text[index] !== " " && text[index] !== "\t") {
      return index;
    }
  }

  return -1;
}

function splitPhysicalLines(text: string): PhysicalLine[] {
  const lines: PhysicalLine[] = [];
  let start = 0;
  let lineNumber = 1;

  for (let index = 0; index < text.length; index += 1) {
    if (text[index] !== "\n") {
      continue;
    }

    let length = index - start;
    if (length > 0 && text[index - 1] === "\r") {
      length -= 1;
    }

    lines.push({
      text: text.slice(start, start + length),
      lineNumber
    });
    lineNumber += 1;
    start = index + 1;
  }

  if (start <= text.length) {
    lines.push({
      text: text.slice(start),
      lineNumber
    });
  }

  return lines;
}

function isIdentifierStart(character: string): boolean {
  return /^[\p{L}_]$/u.test(character);
}

function isIdentifierPart(character: string): boolean {
  return /^[\p{L}\p{N}_]$/u.test(character);
}

function isHexDigit(character: string): boolean {
  return /^[0-9a-fA-F]$/.test(character);
}

function isAsciiDigit(character: string): boolean {
  return /^[0-9]$/.test(character);
}
