export interface UrqlTextLoadOptions {
  encodingName?: string;
}

export interface UrqlTextLoadResult {
  text: string;
  encodingName: string;
  confidence: number;
  bomDetected: boolean;
}

export interface UrqlEncodingCandidate {
  encodingName: string;
  confidence: number;
}

export interface UrqlEncodingDetection {
  encodingName: string;
  confidence: number;
  bomDetected: boolean;
  candidates: UrqlEncodingCandidate[];
}

export function decodeWithAutoDetector(
  bytes: Uint8Array,
  options: UrqlTextLoadOptions,
  autoDetect: (bytes: Uint8Array) => UrqlEncodingDetection
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

  const detected = autoDetect(bytes);
  return {
    text: decodeBytes(bytes, detected.encodingName, detected.encodingName === "utf-8"),
    encodingName: detected.encodingName,
    confidence: detected.confidence,
    bomDetected: detected.bomDetected
  };
}

export function tryDecodeByBom(bytes: Uint8Array): UrqlTextLoadResult | null {
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

export function decodeBytes(bytes: Uint8Array, encodingName: string, fatal: boolean): string {
  return new TextDecoder(resolveTextDecoderEncoding(encodingName), { fatal }).decode(bytes);
}

export function resolveTextDecoderEncoding(encodingName: string): string {
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

export function normalizeEncodingName(name: string): string {
  switch (name.trim().toLowerCase()) {
    case "utf8":
    case "ascii":
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

export function bytesToBinaryString(bytes: Uint8Array): string {
  let result = "";

  for (const byte of bytes) {
    result += String.fromCharCode(byte);
  }

  return result;
}

export function rankCandidates(
  candidates: UrqlEncodingCandidate[],
  preferredOrder: string[] = []
): UrqlEncodingCandidate[] {
  return [...candidates].sort((left, right) => {
    if (right.confidence !== left.confidence) {
      return right.confidence - left.confidence;
    }

    return priorityOf(left.encodingName, preferredOrder) - priorityOf(right.encodingName, preferredOrder);
  });
}

function priorityOf(name: string, preferredOrder: string[]): number {
  const index = preferredOrder.indexOf(name);
  return index >= 0 ? index : preferredOrder.length + 1;
}
