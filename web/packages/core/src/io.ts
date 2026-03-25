import {
  decodeBytes,
  decodeWithAutoDetector,
  type UrqlEncodingCandidate,
  type UrqlEncodingDetection,
  type UrqlTextLoadOptions,
  type UrqlTextLoadResult,
  rankCandidates
} from "./io-common.js";

export { type UrqlEncodingCandidate, type UrqlEncodingDetection, type UrqlTextLoadOptions, type UrqlTextLoadResult } from "./io-common.js";

export class UrqlTextLoader {
  static decode(
    bytes: Uint8Array,
    options: UrqlTextLoadOptions = {}
  ): UrqlTextLoadResult {
    return decodeWithAutoDetector(bytes, options, detectEncodingHeuristically);
  }

  static detectAutoEncoding(bytes: Uint8Array): UrqlEncodingDetection {
    return detectEncodingHeuristically(bytes);
  }

  static async decodeAsync(
    bytes: Uint8Array,
    options: UrqlTextLoadOptions = {}
  ): Promise<UrqlTextLoadResult> {
    return this.decode(bytes, options);
  }

  static async detectAutoEncodingAsync(bytes: Uint8Array): Promise<UrqlEncodingDetection> {
    return this.detectAutoEncoding(bytes);
  }
}

export async function detectEncoding(bytes: Uint8Array): Promise<UrqlEncodingDetection> {
  return detectEncodingHeuristically(bytes);
}

function detectEncodingHeuristically(bytes: Uint8Array): UrqlEncodingDetection {
  const candidates: Array<{ encodingName: string; text: string; score: number }> = [];

  const utf8Text = tryDecodeUtf8Strict(bytes);
  if (utf8Text !== null) {
    candidates.push({
      encodingName: "utf-8",
      text: utf8Text,
      score: scoreText(utf8Text, "utf-8")
    });
  }

  for (const encodingName of ["cp1251", "cp866", "koi8-r"] as const) {
    const text = decodeBytes(bytes, encodingName, false);
    candidates.push({
      encodingName,
      text,
      score: scoreText(text, encodingName)
    });
  }

  const ranked = [...candidates].sort((left, right) => {
    if (right.score !== left.score) {
      return right.score - left.score;
    }

    return heuristicPriority(left.encodingName) - heuristicPriority(right.encodingName);
  });

  let best = ranked[0]!;
  const utf8Candidate = ranked.find((item) => item.encodingName === "utf-8");
  if (utf8Candidate) {
    const nonAsciiBytes = [...bytes].filter((item) => item >= 0x80).length;
    const margin = nonAsciiBytes > 0 ? 0.75 : 0.15;
    if (utf8Candidate.score >= best.score - margin) {
      best = utf8Candidate;
    }
  }

  const normalizedCandidates = rankCandidates(
    candidates.map((item) => ({
      encodingName: item.encodingName,
      confidence: computeConfidence(candidates, item.score)
    })),
    ["utf-8", "cp1251", "cp866", "koi8-r"]
  );

  return {
    encodingName: best.encodingName,
    confidence: computeConfidence(candidates, best.score),
    bomDetected: false,
    candidates: normalizedCandidates
  };
}

function tryDecodeUtf8Strict(bytes: Uint8Array): string | null {
  try {
    return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  } catch {
    return null;
  }
}

function heuristicPriority(name: string): number {
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
  const russianSequenceScore = scoreRussianLetterSequences(text);

  const keywords = /\b(end|if|then|else|goto|proc|btn|instr|print|println|pln)\b/im.test(text)
    ? 0.35
    : 0;

  let baseScore =
    printableRatio * 2 +
    cyrillicRatio * 1.4 +
    russianSequenceScore * 2.2 +
    keywords -
    controlRatio * 2 -
    boxRatio * 4 -
    replacementRatio * 3;

  if (encodingName === "utf-8") {
    baseScore += 0.1;
  }

  return baseScore;
}

const commonRussianBigrams: readonly string[] = [
  "ст", "но", "ен", "то", "на", "ов", "ни", "ра", "ко", "по",
  "пр", "го", "ро", "не", "во", "ть", "ло", "та", "ос", "ал",
  "ли", "от", "ре", "ка", "ер", "де", "ел", "ри", "ес", "ва"
] as const;

const commonRussianTrigrams: readonly string[] = [
  "про", "ени", "ост", "ого", "ать", "ить", "что", "это", "как", "его",
  "ого", "под", "при", "для", "тер", "ени", "ова", "ста", "ник", "ать"
] as const;

function scoreRussianLetterSequences(text: string): number {
  const normalized = text.toLowerCase();
  const lettersOnly = [...normalized].filter((char) => /[а-яё]/u.test(char)).join("");

  if (lettersOnly.length < 8) {
    return 0;
  }

  let bigramHits = 0;
  let trigramHits = 0;
  let bigramWindows = 0;
  let trigramWindows = 0;

  for (let index = 0; index < lettersOnly.length - 1; index += 1) {
    bigramWindows += 1;
    if (commonRussianBigrams.includes(lettersOnly.slice(index, index + 2))) {
      bigramHits += 1;
    }
  }

  for (let index = 0; index < lettersOnly.length - 2; index += 1) {
    trigramWindows += 1;
    if (commonRussianTrigrams.includes(lettersOnly.slice(index, index + 3))) {
      trigramHits += 1;
    }
  }

  const bigramScore = bigramWindows > 0 ? bigramHits / bigramWindows : 0;
  const trigramScore = trigramWindows > 0 ? trigramHits / trigramWindows : 0;
  return bigramScore * 0.65 + trigramScore * 0.35;
}

function computeConfidence(
  candidates: Array<{ encodingName: string; text: string; score: number }>,
  score: number
): number {
  if (candidates.length <= 1) {
    return 1;
  }

  const sortedScores = [...candidates]
    .map((item) => item.score)
    .sort((left, right) => right - left);
  const bestScore = sortedScores[0] ?? score;
  const secondScore = sortedScores[1] ?? 0;

  if (score < bestScore) {
    return Math.max(0.01, Math.min(0.49, 0.5 - Math.max(0, bestScore - score)));
  }

  const gap = Math.max(0, bestScore - secondScore);
  return Math.min(1, 0.5 + gap);
}
