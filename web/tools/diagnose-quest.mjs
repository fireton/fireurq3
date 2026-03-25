import { mkdirSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(scriptDir, "..");
const coreTsconfigPath = resolve(webRoot, "packages/core/tsconfig.json");
const questPath = parseQuestPath(process.argv.slice(2));
const strict = process.argv.includes("--strict");

if (!questPath) {
  printUsageAndExit();
}

const tempRoot = join(webRoot, ".tmp");
mkdirSync(tempRoot, { recursive: true });
const tempOutDir = mkdtempSync(join(tempRoot, "fireurq3-core-diag-"));

try {
  buildCoreToTemp(tempOutDir);

  const [core, io] = await Promise.all([
    import(pathToFileURL(join(tempOutDir, "src/index.js")).href),
    import(pathToFileURL(join(tempOutDir, "src/io.js")).href)
  ]);
  const bytes = new Uint8Array(readFileSync(questPath));
  const load = await io.UrqlTextLoader.decodeAsync(bytes, { encodingName: "auto" });
  const detection = await io.UrqlTextLoader.detectAutoEncodingAsync(bytes);
  const parse = core.Parser.parse(load.text, {
    allowUnknownCommands: !strict
  });
  const ir = core.Compiler.compile(parse.program, parse.diagnostics);

  printSummary({
    questPath,
    strict,
    encodingName: load.encodingName,
    confidence: load.confidence,
    bomDetected: load.bomDetected,
    lineCount: parse.program.lines.length,
    parseCount: parse.diagnostics.length,
    compilerCount: ir.diagnostics.length
  });

  printCandidates(detection.candidates);

  const lines = load.text.split(/\r?\n/u);

  printDiagnostics("Parse Diagnostics", parse.diagnostics, lines);
  printDiagnostics("Compiler Diagnostics", ir.diagnostics, lines);
} finally {
  rmSync(tempOutDir, { recursive: true, force: true });
}

function buildCoreToTemp(outDir) {
  execFileSync(
    join(webRoot, "node_modules/.bin/tsc"),
    [
      "-p",
      coreTsconfigPath,
      "--noEmit",
      "false",
      "--outDir",
      outDir
    ],
    {
      cwd: webRoot,
      stdio: "inherit"
    }
  );
}

function parseQuestPath(argv) {
  const firstValue = argv.find((item) => !item.startsWith("--"));
  return firstValue ? resolve(process.cwd(), firstValue) : null;
}

function printUsageAndExit() {
  console.error("Usage: npm run diagnose:quest -- <path-to-quest.qst> [--strict]");
  process.exit(1);
}

function printSummary(summary) {
  console.log("Quest Summary");
  console.log(`  Path: ${summary.questPath}`);
  console.log(`  Strict Unknown Commands: ${summary.strict ? "yes" : "no"}`);
  console.log(`  Encoding: ${summary.encodingName}`);
  console.log(`  Confidence: ${summary.confidence.toFixed(2)}`);
  console.log(`  BOM: ${summary.bomDetected ? "yes" : "no"}`);
  console.log(`  Lines: ${summary.lineCount}`);
  console.log(`  Parse Diagnostics: ${summary.parseCount}`);
  console.log(`  Compiler Diagnostics: ${summary.compilerCount}`);
  console.log("");
}

function printCandidates(candidates) {
  console.log("Encoding Candidates");

  if (candidates.length === 0) {
    console.log("  none");
    console.log("");
    return;
  }

  for (const candidate of candidates) {
    console.log(`  ${candidate.encodingName}: ${candidate.confidence.toFixed(2)}`);
  }

  console.log("");
}

function printDiagnostics(title, diagnostics, lines) {
  console.log(title);

  if (diagnostics.length === 0) {
    console.log("  none");
    console.log("");
    return;
  }

  for (const diagnostic of diagnostics) {
    const line = lines[diagnostic.span.start.line - 1] ?? "";
    console.log(
      `  [${diagnostic.severity}] ${diagnostic.code} ${formatSpan(diagnostic.span)} ${diagnostic.message}`
    );
    if (line.length > 0) {
      console.log(`    ${line}`);
      console.log(`    ${buildCaretLine(diagnostic.span.start.column, diagnostic.span.end.column)}`);
    }
  }

  console.log("");
}

function formatSpan(span) {
  return `${span.start.line}:${span.start.column}`;
}

function buildCaretLine(startColumn, endColumn) {
  const width = Math.max(1, endColumn - startColumn + 1);
  return `${" ".repeat(Math.max(0, startColumn - 1))}${"^".repeat(width)}`;
}
