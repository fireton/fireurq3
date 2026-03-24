import { describe, expect, it } from "vitest";
import { Parser, parseDiagnosticCode } from "../src/parser.js";

describe("Parser.parseExpressionText", () => {
  it("parses an expression without errors", () => {
    const result = Parser.parseExpressionText("1+2*3");

    expect(result.diagnostics.filter((item) => item.severity === "error")).toHaveLength(0);
    expect(result.expression.kind).toBe("BinaryExpression");
  });

  it("reports trailing tokens in expression text", () => {
    const result = Parser.parseExpressionText("1 2");

    expect(
      result.diagnostics.some(
        (item) => item.code === parseDiagnosticCode.unexpectedToken && item.severity === "error"
      )
    ).toBe(true);
  });
});
