import { diagnosticCode } from "./diagnostics.js";
import { InterpolationTemplateParser } from "./interpolation-template-parser.js";
import { Parser } from "./parser.js";
import { EvalContext, ExpressionEvaluator, type UrqlValue } from "./runtime.js";
import { sourcePosition, sourceSpan } from "./source.js";

export class InterpolationExpander {
  static expandInterpolations(input: string | null | undefined, context: EvalContext): string {
    let current = input ?? "";
    current = expandSpecialInterpolationForms(current, context);

    for (let pass = 0; pass < 16; pass += 1) {
      const state = { hadInterpolation: false };
      const template = InterpolationTemplateParser.parse(current, context.diagnostics, state);
      if (!state.hadInterpolation) {
        return current;
      }

      let next = renderTemplate(template, context);
      next = expandSpecialInterpolationForms(next, context);
      if (next === current) {
        return next;
      }

      current = next;
    }

    context.diagnostics.report(
      diagnosticCode.unexpectedCharacter,
      "warning",
      "Interpolation expansion reached maximum pass count (16).",
      sourceSpan(sourcePosition(1, 1), sourcePosition(1, 1))
    );
    return current;
  }
}

function renderTemplate(
  template: import("./interpolation-template.js").InterpolationTemplate,
  context: EvalContext
): string {
  let output = "";

  for (const part of template.parts) {
    if (part.kind === "LiteralTextPart") {
      output += part.text;
      continue;
    }

    const expressionText = renderTemplate(part.content, context);
    const percentStringValue = tryRenderPercentStringInterpolation(expressionText, context);
    if (percentStringValue !== null) {
      output += percentStringValue;
      continue;
    }

    const expressionParse = Parser.parseExpressionText(expressionText);
    if (expressionParse.diagnostics.some((item) => item.severity === "error")) {
      context.diagnostics.report(
        diagnosticCode.unexpectedCharacter,
        "error",
        `Failed to parse interpolation expression '${expressionText}'.`,
        part.span
      );
      continue;
    }

    const value = ExpressionEvaluator.evaluate(expressionParse.expression, context);
    output += formatNumericInterpolation(value, context);
  }

  return output;
}

function tryRenderPercentStringInterpolation(
  expressionText: string,
  context: EvalContext
): string | null {
  if (expressionText.trim().length === 0) {
    return null;
  }

  if (!expressionText.startsWith("%")) {
    return null;
  }

  const variableName = expressionText.slice(1).trim();
  if (variableName.length === 0 || !isIdentifier(variableName)) {
    return null;
  }

  return context.toUrqlString(context.variables.getOrDefault(variableName));
}

function isIdentifier(value: string): boolean {
  if (value.length === 0) {
    return false;
  }

  if (!/^[\p{L}_]/u.test(value[0]!)) {
    return false;
  }

  return /^[\p{L}\p{N}_]+$/u.test(value);
}

function formatNumericInterpolation(value: UrqlValue, context: EvalContext): string {
  const numeric = value.kind === "Bool" ? (value.boolValue ? 1 : 0) : context.toNumber(value);
  return Number.isInteger(numeric) ? numeric.toString() : numeric.toString();
}

function expandSpecialInterpolationForms(input: string, context: EvalContext): string {
  if (input.length === 0) {
    return "";
  }

  let output = "";
  for (let index = 0; index < input.length; ) {
    if (input[index] !== "#") {
      output += input[index];
      index += 1;
      continue;
    }

    let next = index + 1;
    while (next < input.length && (input[next] === " " || input[next] === "\t")) {
      next += 1;
    }

    if (next < input.length && input[next] === "$") {
      output += " ";
      index = next + 1;
      continue;
    }

    if (next < input.length && input[next] === "/") {
      next += 1;
      while (next < input.length && (input[next] === " " || input[next] === "\t")) {
        next += 1;
      }

      if (next < input.length && input[next] === "$") {
        output += "\n";
        index = next + 1;
        continue;
      }
    }

    if (next < input.length && input[next] === "#") {
      next += 1;
      while (next < input.length && (input[next] === " " || input[next] === "\t")) {
        next += 1;
      }

      const digitsStart = next;
      while (next < input.length && /[0-9]/.test(input[next]!)) {
        next += 1;
      }

      if (next > digitsStart) {
        const digitsEnd = next;
        while (next < input.length && (input[next] === " " || input[next] === "\t")) {
          next += 1;
        }

        if (next < input.length && input[next] === "$") {
          const codeText = input.slice(digitsStart, digitsEnd);
          const code = Number.parseInt(codeText, 10);
          if (!Number.isNaN(code)) {
            const decoded = context.decodeByteChar(code);
            if (decoded.length > 0 || code === 0) {
              output += decoded;
            } else {
              const range = context.usesUnicodeCharCodes() ? "[0..1114111]" : "[0..255]";
              context.diagnostics.report(
                diagnosticCode.invalidNumberLiteral,
                "warning",
                `Character code '${code}' is out of supported range ${range} in interpolation.`,
                sourceSpan(sourcePosition(1, 1), sourcePosition(1, 1))
              );
            }
          } else {
            context.diagnostics.report(
              diagnosticCode.invalidNumberLiteral,
              "warning",
              `Invalid character code '${codeText}' in interpolation.`,
              sourceSpan(sourcePosition(1, 1), sourcePosition(1, 1))
            );
          }

          index = next + 1;
          continue;
        }
      }
    }

    output += input[index];
    index += 1;
  }

  return output;
}
