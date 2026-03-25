import { Compiler, Parser, UrqlTextLoader } from "@fireurq/core";

export const defaultQuestLibraryBaseUrl = "https://my.questlibrary.com/quests/";

export interface QuestSourceOptions {
  defaultBaseUrl?: string;
}

export interface ResolvedQuestSource {
  kind: "url" | "library";
  request: string;
  url: string;
}

export interface LoadedQuestDocument {
  source: ResolvedQuestSource | { kind: "file"; name: string };
  text: string;
  encodingName: string;
  confidence: number;
  bomDetected: boolean;
  parseDiagnosticsCount: number;
  compilerDiagnosticsCount: number;
  lineCount: number;
}

export function resolveQuestSource(
  request: string,
  options: QuestSourceOptions = {}
): ResolvedQuestSource {
  const normalized = request.trim();
  if (!normalized) {
    throw new Error("Quest source request is empty.");
  }

  if (/^https?:\/\//i.test(normalized)) {
    return {
      kind: "url",
      request: normalized,
      url: normalized
    };
  }

  const base = options.defaultBaseUrl ?? defaultQuestLibraryBaseUrl;
  const url = new URL(normalized, ensureTrailingSlash(base)).toString();
  return {
    kind: "library",
    request: normalized,
    url
  };
}

export async function loadQuestFromRequest(
  request: string,
  options: QuestSourceOptions = {}
): Promise<LoadedQuestDocument> {
  const source = resolveQuestSource(request, options);
  const response = await fetch(source.url);
  if (!response.ok) {
    throw new Error(`Failed to load quest from ${source.url} (${response.status}).`);
  }

  const bytes = new Uint8Array(await response.arrayBuffer());
  return buildLoadedDocument(source, bytes);
}

export async function loadQuestFromFile(file: File): Promise<LoadedQuestDocument> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  return buildLoadedDocument(
    {
      kind: "file",
      name: file.name
    },
    bytes
  );
}

async function buildLoadedDocument(
  source: ResolvedQuestSource | { kind: "file"; name: string },
  bytes: Uint8Array
): Promise<LoadedQuestDocument> {
  const load = await UrqlTextLoader.decodeAsync(bytes, { encodingName: "auto" });
  const parse = Parser.parse(load.text);
  const ir = Compiler.compile(parse.program, parse.diagnostics);

  return {
    source,
    text: load.text,
    encodingName: load.encodingName,
    confidence: load.confidence,
    bomDetected: load.bomDetected,
    parseDiagnosticsCount: parse.diagnostics.length,
    compilerDiagnosticsCount: ir.diagnostics.length,
    lineCount: parse.program.lines.length
  };
}

function ensureTrailingSlash(value: string): string {
  return value.endsWith("/") ? value : `${value}/`;
}
