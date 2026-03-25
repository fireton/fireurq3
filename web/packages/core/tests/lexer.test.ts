import { describe, expect, it } from "vitest";
import { diagnosticCode } from "../src/diagnostics.js";
import { Lexer } from "../src/lexer.js";
import { tokenKind } from "../src/token.js";

describe("Lexer", () => {
  it("recognizes core keywords and symbols", () => {
    const source = `
:start
if a<>1 then p "x" & btn next,Go else goto end
end
`.trim();

    const result = Lexer.lex(source);
    const kinds = result.tokens.map((token) => token.kind);

    expect(result.diagnostics).toHaveLength(0);
    expect(kinds).toContain(tokenKind.colon);
    expect(kinds).toContain(tokenKind.identifier);
    expect(kinds).toContain(tokenKind.keywordIf);
    expect(kinds).toContain(tokenKind.notEquals);
    expect(kinds).toContain(tokenKind.keywordThen);
    expect(kinds).toContain(tokenKind.keywordP);
    expect(kinds).toContain(tokenKind.ampersand);
    expect(kinds).toContain(tokenKind.keywordBtn);
    expect(kinds).toContain(tokenKind.comma);
    expect(kinds).toContain(tokenKind.keywordElse);
    expect(kinds).toContain(tokenKind.keywordGoto);
    expect(kinds).toContain(tokenKind.keywordEnd);
    expect(result.tokens.at(-1)?.kind).toBe(tokenKind.endOfFile);
  });

  it("skips single-line and block comments", () => {
    const source = `
; whole line comment
:start ; inline comment
/* block
   comment */
p "ok"
`.trim();

    const result = Lexer.lex(source);
    const texts = result.tokens.map((token) => token.text);

    expect(result.diagnostics).toHaveLength(0);
    expect(texts.join("")).toContain(":start");
    expect(texts).toContain("p");
    expect(texts).toContain("\"ok\"");
    expect(texts).not.toContain("comment");
  });

  it("merges line continuation when underscore starts the line", () => {
    const source = `
p "hello"
    _ & pln "world"
end
`.trim();

    const result = Lexer.lex(source);
    const kinds = result.tokens.map((token) => token.kind);

    expect(result.diagnostics).toHaveLength(0);
    expect(kinds).toContain(tokenKind.ampersand);
    expect(kinds).toContain(tokenKind.keywordPln);
    expect(kinds).toContain(tokenKind.keywordEnd);
  });

  it("parses string escapes", () => {
    const result = Lexer.lex(String.raw`instr s="a\n\x41\""`);
    const stringToken = result.tokens.find((token) => token.kind === tokenKind.string);

    expect(result.diagnostics).toHaveLength(0);
    expect(stringToken?.value).toBe("a\nA\"");
  });

  it("parses int, float, and hex numeric literals", () => {
    const result = Lexer.lex("a=1 b=2.5 c=0xFF");
    const numbers = result.tokens.filter((token) => token.kind === tokenKind.number);

    expect(result.diagnostics).toHaveLength(0);
    expect(numbers).toHaveLength(3);
    expect(numbers[0]?.value).toBe(1);
    expect(numbers[1]?.value).toBe(2.5);
    expect(numbers[2]?.value).toBe(255);
  });

  it("reports unexpected characters", () => {
    const result = Lexer.lex("@");

    expect(result.diagnostics).toHaveLength(0);
    expect(result.tokens.some((token) => token.kind === tokenKind.at && token.text === "@")).toBe(true);
  });

  it("reports unterminated strings", () => {
    const result = Lexer.lex('p "unterminated');

    expect(result.diagnostics.some((item) => item.code === diagnosticCode.unterminatedString)).toBe(true);
  });

  it("reports unterminated block comments", () => {
    const result = Lexer.lex("/* missing end");

    expect(
      result.diagnostics.some((item) => item.code === diagnosticCode.unterminatedBlockComment)
    ).toBe(true);
  });

  it("reports invalid escape sequences", () => {
    const result = Lexer.lex(String.raw`p "bad\q"`);

    expect(
      result.diagnostics.some((item) => item.code === diagnosticCode.invalidEscapeSequence)
    ).toBe(true);
  });

  it("reports invalid hex numbers", () => {
    const result = Lexer.lex("x=0x");

    expect(result.diagnostics.some((item) => item.code === diagnosticCode.invalidNumberLiteral)).toBe(
      true
    );
  });

  it("keeps sentence punctuation in raw text", () => {
    const result = Lexer.lex("pln Привет. Пока!");

    expect(
      result.diagnostics.some((item) => item.code === diagnosticCode.unexpectedCharacter)
    ).toBe(false);
    expect(result.tokens.some((token) => token.kind === tokenKind.dot && token.text === ".")).toBe(true);
    expect(
      result.tokens.some(
        (token) => token.kind === tokenKind.exclamation && token.text === "!"
      )
    ).toBe(true);
  });

  it("keeps email punctuation in raw text", () => {
    const result = Lexer.lex("pln test@example.com");

    expect(
      result.diagnostics.some((item) => item.code === diagnosticCode.unexpectedCharacter)
    ).toBe(false);
    expect(result.tokens.some((token) => token.kind === tokenKind.at && token.text === "@")).toBe(true);
  });
});
