import {
  Compiler,
  EvalContext,
  Parser,
  VirtualMachine,
  vmStatus,
  type Diagnostic,
  type VmStatus
} from "@fireurq/core";
import type { LoadedQuestDocument } from "./source.js";
import type { FrameButton, PlayerFrame, PlayerTextRun } from "./player-frame.js";
import { ViewportMapper } from "./viewport.js";

export interface PlayerSessionConfig {
  virtualWidth?: number;
  virtualHeight?: number;
  maxInstructionsPerAdvance?: number;
  strictUnknownCommands?: boolean;
}

export class PlayerSession {
  private vm: VirtualMachine | null = null;
  private diagnostics: Diagnostic[] = [];
  private textRuns: PlayerTextRun[] = [];
  private consumedOutputLength = 0;
  private hitInstructionLimit = false;
  private status: VmStatus = vmStatus.halted;
  private config: Required<PlayerSessionConfig> = defaultConfig();

  load(document: LoadedQuestDocument, config: PlayerSessionConfig = {}): void {
    this.config = {
      ...defaultConfig(),
      ...config
    };
    this.vm = null;
    this.diagnostics = [];
    this.textRuns = [];
    this.consumedOutputLength = 0;
    this.hitInstructionLimit = false;
    this.status = vmStatus.halted;

    const parse = Parser.parse(document.text, {
      allowUnknownCommands: !this.config.strictUnknownCommands
    });
    const ir = Compiler.compile(parse.program, parse.diagnostics);

    this.diagnostics.push(...parse.diagnostics, ...ir.diagnostics);

    const context = new EvalContext();
    context.charCodeEncodingName = document.encodingName;
    this.vm = new VirtualMachine(ir, context);
    this.status = this.vm.status;
  }

  advance(viewWidth: number, viewHeight: number): PlayerFrame {
    if (!this.vm) {
      return this.snapshot(viewWidth, viewHeight);
    }

    if (
      this.vm.status === vmStatus.waitingForChoice ||
      this.vm.status === vmStatus.halted ||
      this.vm.status === vmStatus.faulted
    ) {
      this.syncOutput();
      this.status = this.vm.status;
      return this.snapshot(viewWidth, viewHeight);
    }

    const run = this.vm.runUntilWaitOrHalt(this.config.maxInstructionsPerAdvance);
    this.hitInstructionLimit = run.hitInstructionLimit;
    this.syncOutput();
    this.status = run.status;
    return this.snapshot(viewWidth, viewHeight);
  }

  selectButton(buttonId: number, viewWidth: number, viewHeight: number): PlayerFrame {
    if (!this.vm || this.vm.status !== vmStatus.waitingForChoice) {
      return this.snapshot(viewWidth, viewHeight);
    }

    const button = this.vm.buttons.find((item) => item.id === buttonId);
    if (!button) {
      return this.snapshot(viewWidth, viewHeight);
    }

    this.textRuns.push({
      kind: "text",
      text: `\n[${button.caption}]\n`
    });

    const chosen = this.vm.chooseButton(buttonId);
    if (!chosen) {
      this.status = this.vm.status;
      return this.snapshot(viewWidth, viewHeight);
    }

    const run = this.vm.runUntilWaitOrHalt(this.config.maxInstructionsPerAdvance);
    this.hitInstructionLimit = run.hitInstructionLimit;
    this.syncOutput();
    this.status = run.status;
    return this.snapshot(viewWidth, viewHeight);
  }

  snapshot(viewWidth: number, viewHeight: number): PlayerFrame {
    const transform = ViewportMapper.computeLetterbox(
      this.config.virtualWidth,
      this.config.virtualHeight,
      Math.max(1, viewWidth),
      Math.max(1, viewHeight)
    );

    return {
      virtualWidth: this.config.virtualWidth,
      virtualHeight: this.config.virtualHeight,
      viewTransform: transform,
      textRuns: [...this.textRuns],
      buttons: this.getButtons(),
      diagnostics: this.collectDiagnostics(),
      status: this.status,
      hitInstructionLimit: this.hitInstructionLimit
    };
  }

  private collectDiagnostics(): Diagnostic[] {
    const diagnostics = [...this.diagnostics];
    if (this.vm) {
      diagnostics.push(...this.vm.context.diagnostics.items);
    }

    return diagnostics;
  }

  private getButtons(): FrameButton[] {
    return this.vm?.buttons.map((button) => ({
      id: button.id,
      caption: button.caption,
      target: button.target
    })) ?? [];
  }

  private syncOutput(): void {
    if (!this.vm || this.consumedOutputLength >= this.vm.outputText.length) {
      return;
    }

    const delta = this.vm.outputText.slice(this.consumedOutputLength);
    this.consumedOutputLength = this.vm.outputText.length;
    this.textRuns.push({
      kind: "text",
      text: delta
    });
  }
}

function defaultConfig(): Required<PlayerSessionConfig> {
  return {
    virtualWidth: 800,
    virtualHeight: 600,
    maxInstructionsPerAdvance: 10_000,
    strictUnknownCommands: false
  };
}
