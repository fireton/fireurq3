import { describe, expect, it } from "vitest";
import type { ExpressionSyntax } from "../src/ast.js";
import { Parser } from "../src/parser.js";
import {
  EvalContext,
  ExpressionEvaluator,
  urqlValue,
  valueKind
} from "../src/runtime.js";

describe("ExpressionEvaluator", () => {
  it.each([
    ["1+2*3", 7],
    ["(1+2)*3", 9],
    ["10/2+1", 6]
  ])("handles arithmetic precedence for %s", (exprText, expected) => {
    const expr = parseExpr(exprText);
    const context = new EvalContext();

    const value = ExpressionEvaluator.evaluate(expr, context);

    expect(value.kind).toBe(valueKind.number);
    expect(value.numberValue).toBe(expected);
  });

  it("handles boolean and comparisons", () => {
    const expr = parseExpr("1<2 and not 0");
    const context = new EvalContext();

    const value = ExpressionEvaluator.evaluate(expr, context);

    expect(value.kind).toBe(valueKind.bool);
    expect(value.boolValue).toBe(true);
  });

  it("resolves variables case-insensitively", () => {
    const expr = parseExpr("Money+2");
    const context = new EvalContext();
    context.variables.set("money", urqlValue.number(5));

    const value = ExpressionEvaluator.evaluate(expr, context);

    expect(value.numberValue).toBe(7);
  });

  it("supports wildcard double equals", () => {
    const expr = parseExpr('"пароль"=="*рол*"');
    const context = new EvalContext();
    const value = ExpressionEvaluator.evaluate(expr, context);

    expect(value.kind).toBe(valueKind.bool);
    expect(value.boolValue).toBe(true);
  });

  it("falls back to inventory counts for bare identifiers", () => {
    const expr = parseExpr("Веревка+2");
    const context = new EvalContext();
    context.inventory.setCount("веревка", 3);

    const value = ExpressionEvaluator.evaluate(expr, context);

    expect(value.kind).toBe(valueKind.number);
    expect(value.numberValue).toBe(5);
  });

  it("reads inv_ prefix through the inventory bridge", () => {
    const expr = parseExpr("inv_Гайка");
    const context = new EvalContext();
    context.inventory.setCount("гайка", 4);

    const value = ExpressionEvaluator.evaluate(expr, context);

    expect(value.kind).toBe(valueKind.number);
    expect(value.numberValue).toBe(4);
  });
});

function parseExpr(text: string): ExpressionSyntax {
  const parsed = Parser.parseExpressionText(text);
  expect(parsed.diagnostics.filter((item) => item.severity === "error")).toHaveLength(0);
  return parsed.expression;
}
