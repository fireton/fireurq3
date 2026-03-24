import type { SourceSpan } from "./source.js";
import type { TokenKind } from "./token.js";

export interface SyntaxNode {
  span: SourceSpan;
}

export interface ProgramSyntax extends SyntaxNode {
  kind: "Program";
  lines: StatementLineSyntax[];
}

export interface StatementLineSyntax extends SyntaxNode {
  kind: "StatementLine";
  label: LabelSyntax | null;
  statements: StatementSyntax[];
}

export interface LabelSyntax extends SyntaxNode {
  kind: "Label";
  name: string;
}

export type StatementSyntax =
  | AssignmentStatementSyntax
  | InstrStatementSyntax
  | IfStatementSyntax
  | GotoStatementSyntax
  | ProcStatementSyntax
  | EndStatementSyntax
  | PrintStatementSyntax
  | BtnStatementSyntax
  | PerkillStatementSyntax
  | InvkillStatementSyntax
  | InvAddStatementSyntax
  | InvRemoveStatementSyntax
  | InvalidStatementSyntax
  | UnknownCommandStatementSyntax;

export interface AssignmentStatementSyntax extends SyntaxNode {
  kind: "AssignmentStatement";
  name: string;
  expression: ExpressionSyntax;
}

export interface InstrStatementSyntax extends SyntaxNode {
  kind: "InstrStatement";
  name: string;
  expression: ExpressionSyntax;
}

export interface IfStatementSyntax extends SyntaxNode {
  kind: "IfStatement";
  condition: ExpressionSyntax;
  thenStatements: StatementSyntax[];
  elseStatements: StatementSyntax[] | null;
}

export interface GotoStatementSyntax extends SyntaxNode {
  kind: "GotoStatement";
  target: ExpressionSyntax;
}

export interface ProcStatementSyntax extends SyntaxNode {
  kind: "ProcStatement";
  target: ExpressionSyntax;
}

export interface EndStatementSyntax extends SyntaxNode {
  kind: "EndStatement";
}

export interface PrintStatementSyntax extends SyntaxNode {
  kind: "PrintStatement";
  textExpression: ExpressionSyntax;
  appendNewLine: boolean;
}

export interface BtnStatementSyntax extends SyntaxNode {
  kind: "BtnStatement";
  targetExpression: ExpressionSyntax;
  captionExpression: ExpressionSyntax;
}

export interface PerkillStatementSyntax extends SyntaxNode {
  kind: "PerkillStatement";
}

export interface InvkillStatementSyntax extends SyntaxNode {
  kind: "InvkillStatement";
  itemExpression: ExpressionSyntax | null;
}

export interface InvAddStatementSyntax extends SyntaxNode {
  kind: "InvAddStatement";
  countExpression: ExpressionSyntax | null;
  itemExpression: ExpressionSyntax;
}

export interface InvRemoveStatementSyntax extends SyntaxNode {
  kind: "InvRemoveStatement";
  countExpression: ExpressionSyntax | null;
  itemExpression: ExpressionSyntax;
}

export interface InvalidStatementSyntax extends SyntaxNode {
  kind: "InvalidStatement";
  reason: string;
}

export interface UnknownCommandStatementSyntax extends SyntaxNode {
  kind: "UnknownCommandStatement";
  commandName: string;
  argumentsExpression: ExpressionSyntax | null;
}

export type ExpressionSyntax =
  | NumberLiteralExpressionSyntax
  | StringLiteralExpressionSyntax
  | IdentifierExpressionSyntax
  | UnaryExpressionSyntax
  | BinaryExpressionSyntax
  | ParenthesizedExpressionSyntax
  | RawTextExpressionSyntax;

export interface NumberLiteralExpressionSyntax extends SyntaxNode {
  kind: "NumberLiteralExpression";
  value: number;
  rawText: string;
}

export interface StringLiteralExpressionSyntax extends SyntaxNode {
  kind: "StringLiteralExpression";
  value: string;
  rawText: string;
}

export interface IdentifierExpressionSyntax extends SyntaxNode {
  kind: "IdentifierExpression";
  name: string;
}

export interface UnaryExpressionSyntax extends SyntaxNode {
  kind: "UnaryExpression";
  operator: TokenKind;
  operand: ExpressionSyntax;
}

export interface BinaryExpressionSyntax extends SyntaxNode {
  kind: "BinaryExpression";
  left: ExpressionSyntax;
  operator: TokenKind;
  right: ExpressionSyntax;
}

export interface ParenthesizedExpressionSyntax extends SyntaxNode {
  kind: "ParenthesizedExpression";
  inner: ExpressionSyntax;
}

export interface RawTextExpressionSyntax extends SyntaxNode {
  kind: "RawTextExpression";
  rawText: string;
}
