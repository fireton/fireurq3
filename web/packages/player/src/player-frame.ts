import type { Diagnostic } from "@fireurq/core";
import type { VmStatus } from "@fireurq/core";

export interface PlayerTextRun {
  kind: "text";
  text: string;
}

export interface FrameButton {
  id: number;
  caption: string;
  target: string;
}

export interface ViewTransform {
  virtualWidth: number;
  virtualHeight: number;
  viewWidth: number;
  viewHeight: number;
  scale: number;
  offsetX: number;
  offsetY: number;
}

export interface PlayerFrame {
  virtualWidth: number;
  virtualHeight: number;
  viewTransform: ViewTransform;
  textRuns: PlayerTextRun[];
  buttons: FrameButton[];
  diagnostics: Diagnostic[];
  status: VmStatus;
  hitInstructionLimit: boolean;
}
