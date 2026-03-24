import type { ExpressionSyntax } from "./ast.js";
import { diagnosticCode } from "./diagnostics.js";
import { InterpolationExpander } from "./interpolation-expander.js";
import type { IrInstruction, IrProgram } from "./ir.js";
import { EvalContext, ExpressionEvaluator, urqlValue } from "./runtime.js";
import { sourcePosition, sourceSpan } from "./source.js";

export const vmStatus = {
  running: "Running",
  waitingForChoice: "WaitingForChoice",
  halted: "Halted",
  faulted: "Faulted"
} as const;

export type VmStatus = (typeof vmStatus)[keyof typeof vmStatus];

export interface VmRunResult {
  status: VmStatus;
  executedInstructions: number;
  hitInstructionLimit: boolean;
}

export interface ButtonAction {
  id: number;
  caption: string;
  target: string;
}

export class VirtualMachine {
  private readonly program: IrProgram;
  private readonly callStack: number[] = [];
  private readonly buttonsInternal: ButtonAction[] = [];
  private output = "";
  private nextButtonId = 1;
  private returnToWaitingAfterUse = false;

  constructor(program: IrProgram, readonly context: EvalContext = new EvalContext()) {
    this.program = program;
    for (const diagnostic of program.diagnostics) {
      this.context.diagnostics.items.push(diagnostic);
    }
  }

  status: VmStatus = vmStatus.running;
  instructionPointer = 0;

  get outputText(): string {
    return this.output;
  }

  get buttons(): readonly ButtonAction[] {
    return this.buttonsInternal;
  }

  step(): boolean {
    if (this.status !== vmStatus.running) {
      return false;
    }

    if (
      this.instructionPointer < 0 ||
      this.instructionPointer >= this.program.instructions.length
    ) {
      this.status = vmStatus.halted;
      return false;
    }

    const instruction = this.program.instructions[this.instructionPointer]!;
    this.execute(instruction);
    return true;
  }

  runUntilWaitOrHalt(maxInstructions: number): VmRunResult {
    let executed = 0;
    while (executed < maxInstructions && this.status === vmStatus.running) {
      if (!this.step()) {
        break;
      }

      executed += 1;
    }

    return {
      status: this.status,
      executedInstructions: executed,
      hitInstructionLimit: executed >= maxInstructions && this.status === vmStatus.running
    };
  }

  runUntilHalt(maxInstructions: number): VmRunResult {
    let executed = 0;
    while (
      executed < maxInstructions &&
      (this.status === vmStatus.running || this.status === vmStatus.waitingForChoice)
    ) {
      if (this.status === vmStatus.waitingForChoice) {
        break;
      }

      if (!this.step()) {
        break;
      }

      executed += 1;
    }

    return {
      status: this.status,
      executedInstructions: executed,
      hitInstructionLimit: executed >= maxInstructions && this.status === vmStatus.running
    };
  }

  chooseButton(buttonId: number): boolean {
    if (this.status !== vmStatus.waitingForChoice) {
      return false;
    }

    const button = this.buttonsInternal.find((item) => item.id === buttonId);
    if (!button) {
      return false;
    }

    this.context.variables.set("last_btn_caption", urqlValue.string(button.caption));
    this.buttonsInternal.length = 0;

    const index = this.program.labelMap.get(button.target.toLowerCase());
    if (index !== undefined) {
      this.instructionPointer = index;
      this.status = vmStatus.running;
      return true;
    }

    this.context.diagnostics.report(
      diagnosticCode.unknownLabel,
      "warning",
      `Unknown label '${button.target}' selected from button.`,
      sourceSpan(sourcePosition(1, 1), sourcePosition(1, 1))
    );
    this.status = vmStatus.halted;
    return false;
  }

  useInventoryItem(itemName: string, actionName?: string): boolean {
    if (!itemName.trim()) {
      return false;
    }

    if (this.context.inventory.getCount(itemName) <= 0) {
      return false;
    }

    const label = this.resolveUseLabel(itemName, actionName);
    if (!label) {
      return false;
    }

    return this.invokeUseLabel(label);
  }

  invokeUseLabel(labelName: string): boolean {
    if (this.status === vmStatus.halted || this.status === vmStatus.faulted) {
      return false;
    }

    if (!labelName.trim()) {
      return false;
    }

    const index = this.program.labelMap.get(labelName.toLowerCase());
    if (index === undefined) {
      this.context.diagnostics.report(
        diagnosticCode.unknownLabel,
        "warning",
        `Unknown label '${labelName}'.`,
        sourceSpan(sourcePosition(1, 1), sourcePosition(1, 1))
      );
      return false;
    }

    this.callStack.push(this.instructionPointer);
    this.returnToWaitingAfterUse = this.status === vmStatus.waitingForChoice;
    this.instructionPointer = index;
    this.status = vmStatus.running;
    return true;
  }

  private execute(instruction: IrInstruction): void {
    switch (instruction.kind) {
      case "AssignInstruction":
        this.executeAssign(instruction);
        this.instructionPointer += 1;
        return;
      case "PrintInstruction":
        this.executePrint(instruction);
        this.instructionPointer += 1;
        return;
      case "AddButtonInstruction":
        this.executeAddButton(instruction);
        this.instructionPointer += 1;
        return;
      case "PerkillInstruction":
        this.context.variables.clear();
        this.instructionPointer += 1;
        return;
      case "InvkillInstruction":
        this.executeInvkill(instruction);
        this.instructionPointer += 1;
        return;
      case "InvAddInstruction":
        this.executeInvDelta(instruction.countExpression, instruction.itemExpression, true);
        this.instructionPointer += 1;
        return;
      case "InvRemoveInstruction":
        this.executeInvDelta(instruction.countExpression, instruction.itemExpression, false);
        this.instructionPointer += 1;
        return;
      case "JumpInstruction":
        if (
          instruction.targetIndex < 0 ||
          instruction.targetIndex >= this.program.instructions.length
        ) {
          this.status = vmStatus.halted;
        } else {
          this.instructionPointer = instruction.targetIndex;
        }
        return;
      case "JumpDynamicInstruction": {
        const target = this.resolveTarget(instruction.targetExpression);
        const index = this.program.labelMap.get(target.toLowerCase());
        if (index !== undefined) {
          this.instructionPointer = index;
        } else {
          this.reportUnknownLabel(target, instruction.span);
          this.instructionPointer += 1;
        }
        return;
      }
      case "JumpIfFalseInstruction": {
        const condition = ExpressionEvaluator.evaluate(
          instruction.conditionExpression,
          this.context
        );
        if (!this.context.toBool(condition)) {
          this.instructionPointer = instruction.targetIndex;
        } else {
          this.instructionPointer += 1;
        }
        return;
      }
      case "CallInstruction":
        this.callStack.push(this.instructionPointer + 1);
        this.instructionPointer = instruction.targetIndex;
        return;
      case "CallDynamicInstruction": {
        const target = this.resolveTarget(instruction.targetExpression);
        const index = this.program.labelMap.get(target.toLowerCase());
        if (index !== undefined) {
          this.callStack.push(this.instructionPointer + 1);
          this.instructionPointer = index;
        } else {
          this.reportUnknownLabel(target, instruction.span);
          this.instructionPointer += 1;
        }
        return;
      }
      case "ReturnOrHaltInstruction":
        if (this.callStack.length > 0) {
          this.instructionPointer = this.callStack.pop()!;
          if (this.callStack.length === 0 && this.returnToWaitingAfterUse) {
            this.returnToWaitingAfterUse = false;
            this.status = vmStatus.waitingForChoice;
          }
        } else {
          this.status =
            this.buttonsInternal.length > 0 ? vmStatus.waitingForChoice : vmStatus.halted;
        }
        return;
      case "NoOpInstruction":
        this.instructionPointer += 1;
        return;
      default:
        this.status = vmStatus.faulted;
    }
  }

  private executeAssign(instruction: Extract<IrInstruction, { kind: "AssignInstruction" }>): void {
    const value = ExpressionEvaluator.evaluate(instruction.expression, this.context);
    if (instruction.forceString) {
      this.setVariable(instruction.name, urqlValue.string(this.context.toUrqlString(value)));
    } else {
      this.setVariable(instruction.name, value);
    }
  }

  private executePrint(instruction: Extract<IrInstruction, { kind: "PrintInstruction" }>): void {
    const value = ExpressionEvaluator.evaluate(instruction.textExpression, this.context);
    const text = this.context.toUrqlString(value);
    const expanded = InterpolationExpander.expandInterpolations(text, this.context);
    this.appendOutput(expanded, instruction.appendNewLine);
  }

  private executeAddButton(
    instruction: Extract<IrInstruction, { kind: "AddButtonInstruction" }>
  ): void {
    const target = this.resolveTarget(instruction.targetExpression);
    const caption = this.resolveCaption(instruction.captionExpression);
    this.buttonsInternal.push({ id: this.nextButtonId++, caption, target });
  }

  private executeInvkill(
    instruction: Extract<IrInstruction, { kind: "InvkillInstruction" }>
  ): void {
    if (!instruction.itemExpression) {
      this.context.inventory.clear();
      return;
    }

    const item = this.context
      .toInterpolationString(ExpressionEvaluator.evaluate(instruction.itemExpression, this.context))
      .trim();

    if (!item) {
      this.context.inventory.clear();
      return;
    }

    this.context.inventory.clear(item);
  }

  private executeInvDelta(
    countExpression: ExpressionSyntax | null,
    itemExpression: ExpressionSyntax,
    add: boolean
  ): void {
    const item = this.context
      .toInterpolationString(ExpressionEvaluator.evaluate(itemExpression, this.context))
      .trim();
    if (!item) {
      return;
    }

    const count =
      countExpression === null
        ? 1
        : this.context.toNumber(ExpressionEvaluator.evaluate(countExpression, this.context));

    if (add) {
      this.context.inventory.add(item, count);
    } else {
      this.context.inventory.remove(item, count);
    }
  }

  private setVariable(name: string, value: ReturnType<typeof urqlValue.number>): void;
  private setVariable(name: string, value: ReturnType<typeof urqlValue.string>): void;
  private setVariable(name: string, value: ReturnType<typeof urqlValue.bool>): void;
  private setVariable(name: string, value: Parameters<VariableSetter>[1]): void {
    if (name.toLowerCase().startsWith("inv_")) {
      this.context.inventory.setCount(name.slice(4), this.context.toNumber(value));
      return;
    }

    this.context.variables.set(name, value);
  }

  private resolveUseLabel(itemName: string, actionName?: string): string | null {
    const item = itemName.trim();
    const itemUnderscored = item.replaceAll(" ", "_");

    if (!actionName?.trim()) {
      for (const label of [`use_${item}`, `use_${itemUnderscored}`]) {
        if (this.program.labelMap.has(label.toLowerCase())) {
          return label;
        }
      }

      return null;
    }

    const action = actionName.trim();
    const actionUnderscored = action.replaceAll(" ", "_");
    for (const label of [
      `use_${item}_${action}`,
      `use_${item}_${actionUnderscored}`,
      `use_${itemUnderscored}_${action}`,
      `use_${itemUnderscored}_${actionUnderscored}`
    ]) {
      if (this.program.labelMap.has(label.toLowerCase())) {
        return label;
      }
    }

    return null;
  }

  private resolveTarget(expression: ExpressionSyntax): string {
    if (expression.kind === "IdentifierExpression") {
      return InterpolationExpander.expandInterpolations(expression.name, this.context);
    }

    if (expression.kind === "StringLiteralExpression") {
      return InterpolationExpander.expandInterpolations(expression.value, this.context);
    }

    return InterpolationExpander.expandInterpolations(
      this.context.toInterpolationString(ExpressionEvaluator.evaluate(expression, this.context)),
      this.context
    );
  }

  private resolveCaption(expression: ExpressionSyntax): string {
    if (expression.kind === "StringLiteralExpression") {
      return InterpolationExpander.expandInterpolations(expression.value, this.context);
    }

    if (expression.kind === "IdentifierExpression") {
      return InterpolationExpander.expandInterpolations(expression.name, this.context);
    }

    return InterpolationExpander.expandInterpolations(
      this.context.toInterpolationString(ExpressionEvaluator.evaluate(expression, this.context)),
      this.context
    );
  }

  private reportUnknownLabel(label: string, span: ExpressionSyntax["span"]): void {
    this.context.diagnostics.report(
      diagnosticCode.unknownLabel,
      "warning",
      `Unknown label '${label}'.`,
      span
    );
  }

  private appendOutput(text: string, appendNewline: boolean): void {
    this.output += text;
    if (appendNewline) {
      this.output += "\n";
    }
  }
}

type VariableSetter = (name: string, value: ReturnType<typeof urqlValue.number>) => void;
