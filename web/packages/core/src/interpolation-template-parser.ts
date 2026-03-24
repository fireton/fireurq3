import { DiagnosticBag, diagnosticCode } from "./diagnostics.js";
import { sourcePosition, sourceSpan, type SourcePosition, type SourceSpan } from "./source.js";
import type {
  EmbeddedExpressionPart,
  InterpolationTemplate,
  LiteralTextPart,
  TemplatePart
} from "./interpolation-template.js";

interface Frame {
  openIndex: number;
  parts: TemplatePart[];
  literal: string[];
}

export class InterpolationTemplateParser {
  static parse(
    input: string,
    diagnostics: DiagnosticBag,
    out: { hadInterpolation: boolean }
  ): InterpolationTemplate {
    out.hadInterpolation = false;
    const positionMap = buildPositionMap(input);
    const stack: Frame[] = [{ openIndex: -1, parts: [], literal: [] }];

    for (let index = 0; index < input.length; index += 1) {
      const character = input[index]!;
      const current = stack[stack.length - 1]!;

      if (character === "#") {
        out.hadInterpolation = true;
        flushLiteral(current);
        stack.push({ openIndex: index, parts: [], literal: [] });
        continue;
      }

      if (character === "$") {
        if (stack.length === 1) {
          diagnostics.reportError(
            diagnosticCode.unexpectedCharacter,
            "Unmatched interpolation close delimiter '$'.",
            spanAt(positionMap, index, index)
          );
          current.literal.push(character);
          continue;
        }

        out.hadInterpolation = true;
        const finished = stack.pop()!;
        flushLiteral(finished);
        const part: EmbeddedExpressionPart = {
          kind: "EmbeddedExpressionPart",
          content: { parts: finished.parts },
          span: spanAt(positionMap, finished.openIndex, index)
        };
        stack[stack.length - 1]!.parts.push(part);
        continue;
      }

      current.literal.push(character);
    }

    while (stack.length > 1) {
      const unclosed = stack.pop()!;
      diagnostics.reportError(
        diagnosticCode.unexpectedCharacter,
        "Unmatched interpolation open delimiter '#'.",
        spanAt(positionMap, unclosed.openIndex, unclosed.openIndex)
      );
    }

    const root = stack.pop()!;
    flushLiteral(root);
    return { parts: root.parts };
  }
}

function flushLiteral(frame: Frame): void {
  if (frame.literal.length === 0) {
    return;
  }

  const part: LiteralTextPart = {
    kind: "LiteralTextPart",
    text: frame.literal.join("")
  };
  frame.parts.push(part);
  frame.literal.length = 0;
}

function spanAt(positions: SourcePosition[], startIndex: number, endIndex: number): SourceSpan {
  const start =
    startIndex >= 0 && startIndex < positions.length ? positions[startIndex]! : sourcePosition(1, 1);
  const end = endIndex >= 0 && endIndex < positions.length ? positions[endIndex]! : start;
  return sourceSpan(start, end);
}

function buildPositionMap(input: string): SourcePosition[] {
  const map: SourcePosition[] = [];
  let line = 1;
  let column = 1;

  for (const character of input) {
    map.push(sourcePosition(line, column));
    if (character === "\n") {
      line += 1;
      column = 1;
    } else {
      column += 1;
    }
  }

  return map;
}
