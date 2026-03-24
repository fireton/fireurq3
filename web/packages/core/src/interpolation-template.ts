import type { SourceSpan } from "./source.js";

export interface InterpolationTemplate {
  parts: TemplatePart[];
}

export type TemplatePart = LiteralTextPart | EmbeddedExpressionPart;

export interface LiteralTextPart {
  kind: "LiteralTextPart";
  text: string;
}

export interface EmbeddedExpressionPart {
  kind: "EmbeddedExpressionPart";
  content: InterpolationTemplate;
  span: SourceSpan;
}
