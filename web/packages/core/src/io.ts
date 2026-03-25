export interface UrqlTextLoadOptions {
  encodingName?: string;
}

export interface UrqlTextLoadResult {
  text: string;
  encodingName: string;
  confidence: number;
  bomDetected: boolean;
}

export class UrqlTextLoader {
  static decode(
    bytes: Uint8Array,
    options: UrqlTextLoadOptions = {}
  ): UrqlTextLoadResult {
    const requested = (options.encodingName ?? "auto").trim().toLowerCase();

    if (requested !== "auto") {
      const encodingName = normalizeEncodingName(requested);
      return {
        text: decodeBytes(bytes, encodingName, encodingName === "utf-8"),
        encodingName,
        confidence: 1,
        bomDetected: false
      };
    }

    const bomResult = tryDecodeByBom(bytes);
    if (bomResult) {
      return bomResult;
    }

    const candidates: Array<{ name: string; text: string; score: number }> = [];

    const utf8Text = tryDecodeUtf8Strict(bytes);
    if (utf8Text !== null) {
      candidates.push({
        name: "utf-8",
        text: utf8Text,
        score: scoreText(utf8Text, "utf-8")
      });
    }

    for (const name of ["cp1251", "cp866", "koi8-r"] as const) {
      const text = decodeBytes(bytes, name, false);
      candidates.push({
        name,
        text,
        score: scoreText(text, name)
      });
    }

    const ranked = [...candidates].sort((left, right) => {
      if (right.score !== left.score) {
        return right.score - left.score;
      }

      return priorityOf(left.name) - priorityOf(right.name);
    });

    let best = ranked[0]!;
    const utf8Candidate = ranked.find((item) => item.name === "utf-8");
    if (utf8Candidate) {
      const nonAsciiBytes = [...bytes].filter((item) => item >= 0x80).length;
      const margin = nonAsciiBytes > 0 ? 0.75 : 0.15;
      if (utf8Candidate.score >= best.score - margin) {
        best = utf8Candidate;
      }
    }

    return {
      text: best.text,
      encodingName: best.name,
      confidence: computeConfidence(candidates, best.score),
      bomDetected: false
    };
  }
}

function tryDecodeByBom(bytes: Uint8Array): UrqlTextLoadResult | null {
  if (bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) {
    return {
      text: new TextDecoder("utf-8").decode(bytes.slice(3)),
      encodingName: "utf-8",
      confidence: 1,
      bomDetected: true
    };
  }

  if (bytes.length >= 2 && bytes[0] === 0xff && bytes[1] === 0xfe) {
    return {
      text: new TextDecoder("utf-16le").decode(bytes.slice(2)),
      encodingName: "utf-16le",
      confidence: 1,
      bomDetected: true
    };
  }

  if (bytes.length >= 2 && bytes[0] === 0xfe && bytes[1] === 0xff) {
    return {
      text: new TextDecoder("utf-16be").decode(bytes.slice(2)),
      encodingName: "utf-16be",
      confidence: 1,
      bomDetected: true
    };
  }

  return null;
}

function tryDecodeUtf8Strict(bytes: Uint8Array): string | null {
  try {
    return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  } catch {
    return null;
  }
}

function decodeBytes(bytes: Uint8Array, encodingName: string, fatal: boolean): string {
  return new TextDecoder(resolveTextDecoderEncoding(encodingName), { fatal }).decode(bytes);
}

function resolveTextDecoderEncoding(encodingName: string): string {
  switch (normalizeEncodingName(encodingName)) {
    case "cp1251":
      return "windows-1251";
    case "cp866":
      return "ibm866";
    case "koi8-r":
      return "koi8-r";
    case "utf-8":
      return "utf-8";
    case "utf-16le":
      return "utf-16le";
    case "utf-16be":
      return "utf-16be";
    default:
      throw new Error(`Unsupported encoding '${encodingName}'.`);
  }
}

function normalizeEncodingName(name: string): string {
  switch (name.trim().toLowerCase()) {
    case "utf8":
      return "utf-8";
    case "windows-1251":
    case "1251":
    case "cp1251":
      return "cp1251";
    case "ibm866":
    case "866":
    case "cp866":
      return "cp866";
    case "koi8r":
    case "koi8-r":
      return "koi8-r";
    case "utf-16":
      return "utf-16le";
    default:
      return name.trim().toLowerCase();
  }
}

function priorityOf(name: string): number {
  switch (name) {
    case "utf-8":
      return 0;
    case "cp1251":
      return 1;
    case "cp866":
      return 2;
    case "koi8-r":
      return 3;
    default:
      return 9;
  }
}

function scoreText(text: string, encodingName: string): number {
  if (text.length === 0) {
    return 0;
  }

  let printable = 0;
  let control = 0;
  let cyrillic = 0;
  let boxDrawing = 0;
  let replacement = 0;

  for (const character of text) {
    if (character === "\ufffd") {
      replacement += 1;
    }

    if (character === "\r" || character === "\n" || character === "\t" || !/\p{Cc}/u.test(character)) {
      printable += 1;
    } else {
      control += 1;
    }

    if (/[\u0400-\u052f]/u.test(character)) {
      cyrillic += 1;
    }

    if (/[\u2500-\u257f]/u.test(character)) {
      boxDrawing += 1;
    }
  }

  const length = Math.max(1, text.length);
  const printableRatio = printable / length;
  const controlRatio = control / length;
  const cyrillicRatio = cyrillic / length;
  const boxRatio = boxDrawing / length;
  const replacementRatio = replacement / length;

  const keywords = /\b(end|if|then|else|goto|proc|btn|instr|print|println|pln)\b/im.test(text)
    ? 0.35
    : 0;

  let baseScore =
    printableRatio * 2 +
    cyrillicRatio * 1.4 +
    keywords -
    controlRatio * 2 -
    boxRatio * 4 -
    replacementRatio * 3;

  if (encodingName === "utf-8") {
    baseScore += 0.1;
  }

  return baseScore;
}

function computeConfidence(
  candidates: Array<{ name: string; text: string; score: number }>,
  bestScore: number
): number {
  if (candidates.length <= 1) {
    return 1;
  }

  const secondScore = [...candidates]
    .map((item) => item.score)
    .sort((left, right) => right - left)[1] ?? 0;

  const gap = Math.max(0, bestScore - secondScore);
  return Math.min(1, 0.5 + gap);
}
