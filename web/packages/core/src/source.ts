export interface SourcePosition {
  line: number;
  column: number;
}

export interface SourceSpan {
  start: SourcePosition;
  end: SourcePosition;
}

export function sourcePosition(line: number, column: number): SourcePosition {
  return { line, column };
}

export function sourceSpan(
  start: SourcePosition,
  end: SourcePosition
): SourceSpan {
  return { start, end };
}
