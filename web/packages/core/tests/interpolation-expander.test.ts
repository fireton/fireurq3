import { describe, expect, it } from "vitest";
import { InterpolationExpander } from "../src/interpolation-expander.js";
import { EvalContext, urqlValue } from "../src/runtime.js";

describe("InterpolationExpander", () => {
  it("expands simple expressions", () => {
    const context = new EvalContext();
    const result = InterpolationExpander.expandInterpolations("x=#1+2$", context);

    expect(result).toBe("x=3");
    expect(context.diagnostics.items.filter((item) => item.severity === "error")).toHaveLength(0);
  });

  it("expands nested expressions inner to outer", () => {
    const context = new EvalContext();
    context.variables.set("a", urqlValue.number(5));
    context.variables.set("b", urqlValue.number(2));

    const result = InterpolationExpander.expandInterpolations("v=#a+#b$$", context);

    expect(result).toBe("v=7");
    expect(context.diagnostics.items.filter((item) => item.severity === "error")).toHaveLength(0);
  });

  it("works inside quotes", () => {
    const context = new EvalContext();
    context.variables.set("name", urqlValue.string("world"));

    const result = InterpolationExpander.expandInterpolations('p "hello #%name$"', context);

    expect(result).toBe('p "hello world"');
  });

  it("supports legacy percent-prefixed string interpolation", () => {
    const context = new EvalContext();
    context.variables.set("name", urqlValue.string("Korwin"));
    context.variables.set("money", urqlValue.number(100));

    const result = InterpolationExpander.expandInterpolations("n=#%name$,m=#%money$", context);

    expect(result).toBe("n=Korwin,m=");
  });

  it("keeps numeric path for non-percent interpolation", () => {
    const context = new EvalContext();
    context.variables.set("name", urqlValue.string("abcd"));

    const result = InterpolationExpander.expandInterpolations("len=#name$", context);

    expect(result).toBe("len=4");
  });

  it("reports unmatched close delimiter", () => {
    const context = new EvalContext();

    InterpolationExpander.expandInterpolations("x=1$", context);

    expect(
      context.diagnostics.items.some((item) =>
        item.message.includes("Unmatched interpolation close delimiter")
      )
    ).toBe(true);
  });

  it("reports unmatched open delimiter", () => {
    const context = new EvalContext();

    InterpolationExpander.expandInterpolations("x=#1+2", context);

    expect(
      context.diagnostics.items.some((item) =>
        item.message.includes("Unmatched interpolation open delimiter")
      )
    ).toBe(true);
  });

  it("substitutes empty text on bad expressions", () => {
    const context = new EvalContext();

    const result = InterpolationExpander.expandInterpolations("x=#(1+$", context);

    expect(result).toBe("x=");
    expect(
      context.diagnostics.items.some((item) =>
        item.message.includes("Failed to parse interpolation expression")
      )
    ).toBe(true);
  });

  it("supports legacy space shortcut", () => {
    const context = new EvalContext();

    expect(InterpolationExpander.expandInterpolations("a#$b", context)).toBe("a b");
  });

  it("supports legacy newline shortcut", () => {
    const context = new EvalContext();

    expect(InterpolationExpander.expandInterpolations("a#/$b", context)).toBe("a\nb");
  });

  it("supports legacy char code shortcut", () => {
    const context = new EvalContext();

    expect(InterpolationExpander.expandInterpolations("x=##65$!", context)).toBe("x=A!");
  });

  it("uses cp1251 char codes when configured", () => {
    const context = new EvalContext();
    context.charCodeEncodingName = "cp1251";

    expect(InterpolationExpander.expandInterpolations("x=##192$", context)).toBe("x=А");
  });

  it("uses cp866 char codes when configured", () => {
    const context = new EvalContext();
    context.charCodeEncodingName = "cp866";

    expect(InterpolationExpander.expandInterpolations("x=##192$", context)).toBe("x=└");
  });

  it("uses koi8-r char codes when configured", () => {
    const context = new EvalContext();
    context.charCodeEncodingName = "koi8-r";

    expect(InterpolationExpander.expandInterpolations("x=##225$", context)).toBe("x=А");
  });

  it("uses Unicode code points for utf-8", () => {
    const context = new EvalContext();
    context.charCodeEncodingName = "utf-8";

    expect(InterpolationExpander.expandInterpolations("x=##1046$", context)).toBe("x=Ж");
  });
});
