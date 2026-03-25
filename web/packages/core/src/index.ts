export * from "./diagnostics.js";
export * from "./io.js";
export * from "./lexer.js";
export * from "./parser-options.js";
export * from "./parser.js";
export * from "./source.js";
export * from "./ast.js";
export * from "./interpolation-template.js";
export * from "./interpolation-template-parser.js";
export * from "./interpolation-expander.js";
export * from "./ir.js";
export * from "./compiler.js";
export * from "./runtime.js";
export * from "./vm.js";
export * from "./token.js";

export const coreMigrationStatus = {
  summary:
    "The TypeScript core package now includes lexer/parser ports, runtime evaluation, interpolation expansion, and the first executable compiler/VM slice.",
  nextSteps: [
    "Expand compiler/VM coverage until it matches the current C# execution suite",
    "Port dynamic single-statement execution and the remaining runtime helpers"
  ]
} as const;
