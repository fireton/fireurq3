import { DiagnosticBag, diagnosticCode } from "./diagnostics.js";
import type { ExpressionSyntax } from "./ast.js";
import { sourcePosition, sourceSpan } from "./source.js";
import { tokenKind } from "./token.js";

export const valueKind = {
  number: "Number",
  string: "String",
  bool: "Bool"
} as const;

export type ValueKind = (typeof valueKind)[keyof typeof valueKind];

export type UrqlValue =
  | { kind: "Number"; numberValue: number; stringValue: ""; boolValue: false }
  | { kind: "String"; numberValue: 0; stringValue: string; boolValue: false }
  | { kind: "Bool"; numberValue: 0; stringValue: ""; boolValue: boolean };

export const urqlValue = {
  number(value: number): UrqlValue {
    return { kind: "Number", numberValue: value, stringValue: "", boolValue: false };
  },
  string(value: string): UrqlValue {
    return { kind: "String", numberValue: 0, stringValue: value ?? "", boolValue: false };
  },
  bool(value: boolean): UrqlValue {
    return { kind: "Bool", numberValue: 0, stringValue: "", boolValue: value };
  }
} as const;

export class VariableStore {
  private readonly values = new Map<string, UrqlValue>();

  set(name: string, value: UrqlValue): void {
    this.values.set(name.toLowerCase(), value);
  }

  tryGet(name: string): UrqlValue | null {
    return this.values.get(name.toLowerCase()) ?? null;
  }

  getOrDefault(name: string): UrqlValue {
    return this.tryGet(name) ?? urqlValue.number(0);
  }

  clear(): void {
    this.values.clear();
  }
}

export class InventoryStore {
  private readonly items = new Map<string, number>();

  getCount(itemName: string): number {
    const key = normalizeKey(itemName);
    if (!key) {
      return 0;
    }

    return this.items.get(key) ?? 0;
  }

  setCount(itemName: string, count: number): void {
    const key = normalizeKey(itemName);
    if (!key) {
      return;
    }

    if (count <= 0) {
      this.items.delete(key);
      return;
    }

    this.items.set(key, count);
  }

  add(itemName: string, count: number): void {
    if (count === 0) {
      return;
    }

    this.setCount(itemName, this.getCount(itemName) + count);
  }

  remove(itemName: string, count: number): void {
    if (count === 0) {
      return;
    }

    this.setCount(itemName, this.getCount(itemName) - count);
  }

  clear(itemName?: string): void {
    if (itemName === undefined) {
      this.items.clear();
      return;
    }

    const key = normalizeKey(itemName);
    if (!key) {
      return;
    }

    this.items.delete(key);
  }
}

export class EvalContext {
  readonly variables = new VariableStore();
  readonly inventory = new InventoryStore();
  readonly diagnostics = new DiagnosticBag();
  charCodeEncodingName = "cp1251";

  toNumber(value: UrqlValue): number {
    switch (value.kind) {
      case "Number":
        return value.numberValue;
      case "String":
        return value.stringValue.length;
      case "Bool":
        return value.boolValue ? 1 : 0;
    }
  }

  toUrqlString(value: UrqlValue): string {
    switch (value.kind) {
      case "String":
        return value.stringValue;
      case "Number":
        return "";
      case "Bool":
        return value.boolValue ? "1" : "0";
    }
  }

  toInterpolationString(value: UrqlValue): string {
    switch (value.kind) {
      case "String":
        return value.stringValue;
      case "Number":
        return formatNumber(value.numberValue);
      case "Bool":
        return value.boolValue ? "1" : "0";
    }
  }

  toBool(value: UrqlValue): boolean {
    switch (value.kind) {
      case "Number":
        return Math.abs(value.numberValue) > 0;
      case "String":
        return value.stringValue.length > 0;
      case "Bool":
        return value.boolValue;
    }
  }

  decodeByteChar(code: number): string {
    if (this.usesUnicodeCharCodes()) {
      if (code < 0 || code > 0x10ffff || (code >= 0xd800 && code <= 0xdfff)) {
        return "";
      }

      return String.fromCodePoint(code);
    }

    if (code < 0 || code > 255) {
      return "";
    }

    const encoding = this.getCharCodeEncoding();
    return new TextDecoder(encoding).decode(Uint8Array.from([code]));
  }

  usesUnicodeCharCodes(): boolean {
    const normalized = this.charCodeEncodingName.trim().toLowerCase();
    return (
      normalized === "utf-8" ||
      normalized === "utf8" ||
      normalized === "utf-16" ||
      normalized === "utf-16le" ||
      normalized === "utf-16be" ||
      normalized === "unicode"
    );
  }

  private getCharCodeEncoding(): string {
    const normalized = this.charCodeEncodingName.trim().toLowerCase();
    switch (normalized) {
      case "cp1251":
      case "windows-1251":
      case "1251":
        return "windows-1251";
      case "cp866":
      case "866":
      case "ibm866":
        return "ibm866";
      case "koi8-r":
      case "koi8r":
        return "koi8-r";
      case "utf-8":
      case "utf8":
        return "utf-8";
      default:
        return "windows-1251";
    }
  }
}

export class ExpressionEvaluator {
  static evaluate(expression: ExpressionSyntax, context: EvalContext): UrqlValue {
    switch (expression.kind) {
      case "NumberLiteralExpression":
        return urqlValue.number(expression.value);
      case "StringLiteralExpression":
        return urqlValue.string(expression.value);
      case "IdentifierExpression":
        return resolveIdentifier(expression.name, context);
      case "ParenthesizedExpression":
        return this.evaluate(expression.inner, context);
      case "RawTextExpression":
        return urqlValue.string(expression.rawText);
      case "UnaryExpression":
        return evaluateUnary(expression, context);
      case "BinaryExpression":
        return evaluateBinary(expression, context);
    }
  }
}

function resolveIdentifier(name: string, context: EvalContext): UrqlValue {
  if (name.toLowerCase().startsWith("inv_")) {
    return urqlValue.number(context.inventory.getCount(name.slice(4)));
  }

  const variable = context.variables.tryGet(name);
  if (variable) {
    return variable;
  }

  const inventoryCount = context.inventory.getCount(name);
  if (inventoryCount > 0) {
    return urqlValue.number(inventoryCount);
  }

  return urqlValue.number(0);
}

function evaluateUnary(
  expression: Extract<ExpressionSyntax, { kind: "UnaryExpression" }>,
  context: EvalContext
): UrqlValue {
  const operand = ExpressionEvaluator.evaluate(expression.operand, context);

  switch (expression.operator) {
    case tokenKind.plus:
      return urqlValue.number(context.toNumber(operand));
    case tokenKind.minus:
      return urqlValue.number(-context.toNumber(operand));
    case tokenKind.keywordNot:
      return urqlValue.bool(!context.toBool(operand));
    default:
      return urqlValue.number(0);
  }
}

function evaluateBinary(
  expression: Extract<ExpressionSyntax, { kind: "BinaryExpression" }>,
  context: EvalContext
): UrqlValue {
  const left = ExpressionEvaluator.evaluate(expression.left, context);
  const right = ExpressionEvaluator.evaluate(expression.right, context);

  switch (expression.operator) {
    case tokenKind.plus:
      return evaluatePlus(left, right, context);
    case tokenKind.minus:
      return urqlValue.number(context.toNumber(left) - context.toNumber(right));
    case tokenKind.star:
      return urqlValue.number(context.toNumber(left) * context.toNumber(right));
    case tokenKind.slash:
      return urqlValue.number(context.toNumber(left) / context.toNumber(right));
    case tokenKind.percent:
      return urqlValue.number(context.toNumber(left) % context.toNumber(right));
    case tokenKind.equals:
      return urqlValue.bool(eq(left, right, context));
    case tokenKind.notEquals:
      return urqlValue.bool(!eq(left, right, context));
    case tokenKind.doubleEquals:
      return urqlValue.bool(wildcardMatch(context.toUrqlString(left), context.toUrqlString(right)));
    case tokenKind.less:
      return urqlValue.bool(context.toNumber(left) < context.toNumber(right));
    case tokenKind.lessOrEquals:
      return urqlValue.bool(context.toNumber(left) <= context.toNumber(right));
    case tokenKind.greater:
      return urqlValue.bool(context.toNumber(left) > context.toNumber(right));
    case tokenKind.greaterOrEquals:
      return urqlValue.bool(context.toNumber(left) >= context.toNumber(right));
    case tokenKind.keywordAnd:
      return urqlValue.bool(context.toBool(left) && context.toBool(right));
    case tokenKind.keywordOr:
      return urqlValue.bool(context.toBool(left) || context.toBool(right));
    default:
      return urqlValue.number(0);
  }
}

function evaluatePlus(left: UrqlValue, right: UrqlValue, context: EvalContext): UrqlValue {
  if (left.kind === valueKind.string) {
    return urqlValue.string(context.toUrqlString(left) + context.toUrqlString(right));
  }

  return urqlValue.number(context.toNumber(left) + context.toNumber(right));
}

function eq(left: UrqlValue, right: UrqlValue, context: EvalContext): boolean {
  if (left.kind === valueKind.string || right.kind === valueKind.string) {
    return context.toUrqlString(left).localeCompare(context.toUrqlString(right), undefined, {
      sensitivity: "accent"
    }) === 0;
  }

  return Math.abs(context.toNumber(left) - context.toNumber(right)) < 1e-12;
}

function wildcardMatch(input: string, pattern: string): boolean {
  return wildcardMatchCore(input ?? "", pattern ?? "");
}

function wildcardMatchCore(input: string, pattern: string): boolean {
  let inputIndex = 0;
  let patternIndex = 0;
  let starIndex = -1;
  let matchIndex = 0;

  while (inputIndex < input.length) {
    if (
      patternIndex < pattern.length &&
      (pattern[patternIndex] === "?" ||
        input[inputIndex]!.toLowerCase() === pattern[patternIndex]!.toLowerCase())
    ) {
      inputIndex += 1;
      patternIndex += 1;
    } else if (patternIndex < pattern.length && pattern[patternIndex] === "*") {
      starIndex = patternIndex;
      patternIndex += 1;
      matchIndex = inputIndex;
    } else if (starIndex !== -1) {
      patternIndex = starIndex + 1;
      matchIndex += 1;
      inputIndex = matchIndex;
    } else {
      return false;
    }
  }

  while (patternIndex < pattern.length && pattern[patternIndex] === "*") {
    patternIndex += 1;
  }

  return patternIndex === pattern.length;
}

function normalizeKey(value: string): string {
  const normalized = value.trim().toLowerCase();
  return normalized.length > 0 ? normalized : "";
}

function formatNumber(value: number): string {
  if (Number.isInteger(value)) {
    return value.toString();
  }

  return value.toString();
}
