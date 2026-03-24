import type { ExpressionSyntax } from "./ast.js";
import type { Diagnostic } from "./diagnostics.js";
import type { SourceSpan } from "./source.js";

export type IrInstruction =
  | AssignInstruction
  | JumpInstruction
  | JumpDynamicInstruction
  | JumpIfFalseInstruction
  | CallInstruction
  | CallDynamicInstruction
  | ReturnOrHaltInstruction
  | PrintInstruction
  | AddButtonInstruction
  | PerkillInstruction
  | InvkillInstruction
  | InvAddInstruction
  | InvRemoveInstruction
  | NoOpInstruction;

export interface AssignInstruction {
  kind: "AssignInstruction";
  name: string;
  expression: ExpressionSyntax;
  forceString: boolean;
  span: SourceSpan;
}

export interface JumpInstruction {
  kind: "JumpInstruction";
  targetIndex: number;
  span: SourceSpan;
}

export interface JumpDynamicInstruction {
  kind: "JumpDynamicInstruction";
  targetExpression: ExpressionSyntax;
  span: SourceSpan;
}

export interface JumpIfFalseInstruction {
  kind: "JumpIfFalseInstruction";
  conditionExpression: ExpressionSyntax;
  targetIndex: number;
  span: SourceSpan;
}

export interface CallInstruction {
  kind: "CallInstruction";
  targetIndex: number;
  span: SourceSpan;
}

export interface CallDynamicInstruction {
  kind: "CallDynamicInstruction";
  targetExpression: ExpressionSyntax;
  span: SourceSpan;
}

export interface ReturnOrHaltInstruction {
  kind: "ReturnOrHaltInstruction";
  span: SourceSpan;
}

export interface PrintInstruction {
  kind: "PrintInstruction";
  textExpression: ExpressionSyntax;
  appendNewLine: boolean;
  span: SourceSpan;
}

export interface AddButtonInstruction {
  kind: "AddButtonInstruction";
  targetExpression: ExpressionSyntax;
  captionExpression: ExpressionSyntax;
  span: SourceSpan;
}

export interface PerkillInstruction {
  kind: "PerkillInstruction";
  span: SourceSpan;
}

export interface InvkillInstruction {
  kind: "InvkillInstruction";
  itemExpression: ExpressionSyntax | null;
  span: SourceSpan;
}

export interface InvAddInstruction {
  kind: "InvAddInstruction";
  countExpression: ExpressionSyntax | null;
  itemExpression: ExpressionSyntax;
  span: SourceSpan;
}

export interface InvRemoveInstruction {
  kind: "InvRemoveInstruction";
  countExpression: ExpressionSyntax | null;
  itemExpression: ExpressionSyntax;
  span: SourceSpan;
}

export interface NoOpInstruction {
  kind: "NoOpInstruction";
  span: SourceSpan;
}

export interface IrProgram {
  instructions: IrInstruction[];
  labelMap: Map<string, number>;
  diagnostics: Diagnostic[];
}
