import { describe, expect, it } from "vitest";
import { DiagnosticBag, diagnosticCode } from "../src/diagnostics.js";
import { InterpolationTemplateParser } from "../src/interpolation-template-parser.js";

describe("InterpolationTemplateParser", () => {
  it("parses plain literal text without interpolations", () => {
    const diagnostics = new DiagnosticBag();
    const state = { hadInterpolation: false };
    const template = InterpolationTemplateParser.parse("hello", diagnostics, state);

    expect(state.hadInterpolation).toBe(false);
    expect(diagnostics.items).toHaveLength(0);
    expect(template.parts).toEqual([{ kind: "LiteralTextPart", text: "hello" }]);
  });

  it("parses nested interpolations with stack-based structure", () => {
    const diagnostics = new DiagnosticBag();
    const state = { hadInterpolation: false };
    const template = InterpolationTemplateParser.parse("v=#a+#b$$", diagnostics, state);

    expect(state.hadInterpolation).toBe(true);
    expect(diagnostics.items).toHaveLength(0);
    expect(template.parts).toHaveLength(2);
    expect(template.parts[1]?.kind).toBe("EmbeddedExpressionPart");
    if (template.parts[1]?.kind !== "EmbeddedExpressionPart") {
      return;
    }

    expect(template.parts[1].content.parts).toHaveLength(2);
    expect(template.parts[1].content.parts[1]?.kind).toBe("EmbeddedExpressionPart");
  });

  it("reports unmatched close delimiter", () => {
    const diagnostics = new DiagnosticBag();
    const state = { hadInterpolation: false };

    InterpolationTemplateParser.parse("x=1$", diagnostics, state);

    expect(
      diagnostics.items.some((item) => item.code === diagnosticCode.unexpectedCharacter)
    ).toBe(true);
  });

  it("reports unmatched open delimiter", () => {
    const diagnostics = new DiagnosticBag();
    const state = { hadInterpolation: false };

    InterpolationTemplateParser.parse("x=#1+2", diagnostics, state);

    expect(
      diagnostics.items.some((item) => item.code === diagnosticCode.unexpectedCharacter)
    ).toBe(true);
  });
});
