import type { SourceSpan } from "./source.js";

export const tokenKind = {
  endOfFile: "EndOfFile",
  newLine: "NewLine",
  identifier: "Identifier",
  number: "Number",
  string: "String",
  colon: "Colon",
  comma: "Comma",
  ampersand: "Ampersand",
  openParen: "OpenParen",
  closeParen: "CloseParen",
  plus: "Plus",
  minus: "Minus",
  star: "Star",
  slash: "Slash",
  percent: "Percent",
  equals: "Equals",
  doubleEquals: "DoubleEquals",
  notEquals: "NotEquals",
  less: "Less",
  lessOrEquals: "LessOrEquals",
  greater: "Greater",
  greaterOrEquals: "GreaterOrEquals",
  hash: "Hash",
  dollar: "Dollar",
  question: "Question",
  dot: "Dot",
  exclamation: "Exclamation",
  keywordIf: "KeywordIf",
  keywordThen: "KeywordThen",
  keywordElse: "KeywordElse",
  keywordGoto: "KeywordGoto",
  keywordProc: "KeywordProc",
  keywordEnd: "KeywordEnd",
  keywordInstr: "KeywordInstr",
  keywordP: "KeywordP",
  keywordPrint: "KeywordPrint",
  keywordPln: "KeywordPln",
  keywordPrintln: "KeywordPrintln",
  keywordBtn: "KeywordBtn",
  keywordAnd: "KeywordAnd",
  keywordOr: "KeywordOr",
  keywordNot: "KeywordNot"
} as const;

export type TokenKind = (typeof tokenKind)[keyof typeof tokenKind];

export interface Token {
  kind: TokenKind;
  text: string;
  span: SourceSpan;
  value?: number | string;
}

export interface LexResult {
  tokens: Token[];
  diagnostics: import("./diagnostics.js").Diagnostic[];
}
