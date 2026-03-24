import type {
  ExpressionSyntax,
  ProgramSyntax,
  StatementSyntax
} from "./ast.js";
import { DiagnosticBag, diagnosticCode, type Diagnostic } from "./diagnostics.js";
import type { SourceSpan } from "./source.js";
import type { IrInstruction, IrProgram } from "./ir.js";

interface PendingPatch {
  instructionIndex: number;
  labelName: string;
  isCall: boolean;
  span: SourceSpan;
}

export class Compiler {
  static compile(program: ProgramSyntax, parseDiagnostics: Diagnostic[] = []): IrProgram {
    const diagnostics = new DiagnosticBag();
    diagnostics.items.push(...parseDiagnostics);

    const instructions: IrInstruction[] = [];
    const labelMap = new Map<string, number>();
    const pending: PendingPatch[] = [];

    for (const line of program.lines) {
      if (line.label) {
        const key = line.label.name.toLowerCase();
        if (labelMap.has(key)) {
          diagnostics.reportError(
            diagnosticCode.duplicateLabel,
            `Duplicate label '${line.label.name}'.`,
            line.label.span
          );
        } else {
          labelMap.set(key, instructions.length);
        }
      }

      for (const statement of line.statements) {
        emitStatement(statement, instructions, pending);
      }
    }

    for (const patch of pending) {
      const target = labelMap.get(patch.labelName.toLowerCase());
      if (target === undefined) {
        diagnostics.report(
          diagnosticCode.unknownLabel,
          "warning",
          `Unknown label '${patch.labelName}'.`,
          patch.span
        );
        continue;
      }

      instructions[patch.instructionIndex] = patch.isCall
        ? { kind: "CallInstruction", targetIndex: target, span: patch.span }
        : { kind: "JumpInstruction", targetIndex: target, span: patch.span };
    }

    return {
      instructions,
      labelMap,
      diagnostics: diagnostics.items
    };
  }
}

function emitStatement(
  statement: StatementSyntax,
  instructions: IrInstruction[],
  pending: PendingPatch[]
): void {
  switch (statement.kind) {
    case "AssignmentStatement":
      instructions.push({
        kind: "AssignInstruction",
        name: statement.name,
        expression: statement.expression,
        forceString: false,
        span: statement.span
      });
      return;
    case "InstrStatement":
      instructions.push({
        kind: "AssignInstruction",
        name: statement.name,
        expression: statement.expression,
        forceString: true,
        span: statement.span
      });
      return;
    case "PrintStatement":
      instructions.push({
        kind: "PrintInstruction",
        textExpression: statement.textExpression,
        appendNewLine: statement.appendNewLine,
        span: statement.span
      });
      return;
    case "BtnStatement":
      instructions.push({
        kind: "AddButtonInstruction",
        targetExpression: statement.targetExpression,
        captionExpression: statement.captionExpression,
        span: statement.span
      });
      return;
    case "PerkillStatement":
      instructions.push({ kind: "PerkillInstruction", span: statement.span });
      return;
    case "InvkillStatement":
      instructions.push({
        kind: "InvkillInstruction",
        itemExpression: statement.itemExpression,
        span: statement.span
      });
      return;
    case "InvAddStatement":
      instructions.push({
        kind: "InvAddInstruction",
        countExpression: statement.countExpression,
        itemExpression: statement.itemExpression,
        span: statement.span
      });
      return;
    case "InvRemoveStatement":
      instructions.push({
        kind: "InvRemoveInstruction",
        countExpression: statement.countExpression,
        itemExpression: statement.itemExpression,
        span: statement.span
      });
      return;
    case "EndStatement":
      instructions.push({ kind: "ReturnOrHaltInstruction", span: statement.span });
      return;
    case "GotoStatement": {
      const label = tryStaticLabel(statement.target);
      if (label) {
        const instructionIndex = instructions.length;
        instructions.push({ kind: "JumpInstruction", targetIndex: -1, span: statement.span });
        pending.push({ instructionIndex, labelName: label, isCall: false, span: statement.span });
      } else {
        instructions.push({
          kind: "JumpDynamicInstruction",
          targetExpression: statement.target,
          span: statement.span
        });
      }
      return;
    }
    case "ProcStatement": {
      const label = tryStaticLabel(statement.target);
      if (label) {
        const instructionIndex = instructions.length;
        instructions.push({ kind: "CallInstruction", targetIndex: -1, span: statement.span });
        pending.push({ instructionIndex, labelName: label, isCall: true, span: statement.span });
      } else {
        instructions.push({
          kind: "CallDynamicInstruction",
          targetExpression: statement.target,
          span: statement.span
        });
      }
      return;
    }
    case "IfStatement":
      emitIf(statement, instructions, pending);
      return;
    default:
      instructions.push({ kind: "NoOpInstruction", span: statement.span });
  }
}

function emitIf(
  statement: Extract<StatementSyntax, { kind: "IfStatement" }>,
  instructions: IrInstruction[],
  pending: PendingPatch[]
): void {
  const jumpFalseIndex = instructions.length;
  instructions.push({
    kind: "JumpIfFalseInstruction",
    conditionExpression: statement.condition,
    targetIndex: -1,
    span: statement.span
  });

  for (const item of statement.thenStatements) {
    emitStatement(item, instructions, pending);
  }

  if (statement.elseStatements && statement.elseStatements.length > 0) {
    const jumpEndIndex = instructions.length;
    instructions.push({ kind: "JumpInstruction", targetIndex: -1, span: statement.span });

    const elseStart = instructions.length;
    instructions[jumpFalseIndex] = {
      kind: "JumpIfFalseInstruction",
      conditionExpression: statement.condition,
      targetIndex: elseStart,
      span: statement.span
    };

    for (const item of statement.elseStatements) {
      emitStatement(item, instructions, pending);
    }

    instructions[jumpEndIndex] = {
      kind: "JumpInstruction",
      targetIndex: instructions.length,
      span: statement.span
    };
  } else {
    instructions[jumpFalseIndex] = {
      kind: "JumpIfFalseInstruction",
      conditionExpression: statement.condition,
      targetIndex: instructions.length,
      span: statement.span
    };
  }
}

function tryStaticLabel(expression: ExpressionSyntax): string | null {
  switch (expression.kind) {
    case "IdentifierExpression":
      return expression.name.trim() || null;
    case "StringLiteralExpression":
      return expression.value.trim() || null;
    default:
      return null;
  }
}
