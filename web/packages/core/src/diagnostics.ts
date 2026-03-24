import type { SourceSpan } from "./source.js";

export const diagnosticCode = {
  unexpectedCharacter: "URQL1001",
  unterminatedString: "URQL1002",
  unterminatedBlockComment: "URQL1003",
  invalidEscapeSequence: "URQL1004",
  invalidNumberLiteral: "URQL1005",
  expectedToken: "URQL2001",
  unexpectedToken: "URQL2002",
  invalidStatement: "URQL2003",
  unknownCommand: "URQL2004",
  duplicateLabel: "URQL3001",
  unknownLabel: "URQL3002"
} as const;

export type DiagnosticCode =
  (typeof diagnosticCode)[keyof typeof diagnosticCode];

export type DiagnosticSeverity = "info" | "warning" | "error";

export interface Diagnostic {
  code: DiagnosticCode;
  severity: DiagnosticSeverity;
  message: string;
  span: SourceSpan;
}

export class DiagnosticBag {
  readonly items: Diagnostic[] = [];

  get hasErrors(): boolean {
    return this.items.some((item) => item.severity === "error");
  }

  report(
    code: DiagnosticCode,
    severity: DiagnosticSeverity,
    message: string,
    span: SourceSpan
  ): void {
    this.items.push({ code, severity, message, span });
  }

  reportError(code: DiagnosticCode, message: string, span: SourceSpan): void {
    this.report(code, "error", message, span);
  }
}
