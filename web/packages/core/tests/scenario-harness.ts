import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { expect } from "vitest";
import { Compiler } from "../src/compiler.js";
import { UrqlTextLoader } from "../src/io.js";
import { Parser } from "../src/parser.js";
import { EvalContext } from "../src/runtime.js";
import { VirtualMachine, type ButtonAction, vmStatus, type VmStatus } from "../src/vm.js";

export interface ButtonPick {
  caption?: string;
  index?: number;
}

export interface QuestCheckpoint {
  status?: VmStatus;
  numberVariables?: Record<string, number>;
  stringVariables?: Record<string, string>;
  inventory?: Record<string, number>;
  outputContains?: string[];
  hasErrorDiagnostics?: boolean;
}

export interface QuestScenarioStep {
  pick: ButtonPick;
  checkpoint?: QuestCheckpoint;
}

export interface QuestScenario {
  scriptPath: string;
  encodingName?: string;
  maxInstructionsPerRun?: number;
  initialCheckpoint?: QuestCheckpoint;
  steps?: QuestScenarioStep[];
  finalCheckpoint?: QuestCheckpoint;
  name?: string;
}

export interface QuestScenarioResult {
  vm: VirtualMachine;
  detectedEncodingName: string;
  pickedCaptions: string[];
}

export class QuestScenarioHarness {
  static run(scenario: QuestScenario): QuestScenarioResult {
    const bytes = readFileSync(scenario.scriptPath);
    const load = UrqlTextLoader.decode(bytes, {
      encodingName: scenario.encodingName ?? "auto"
    });
    const parse = Parser.parse(load.text);
    const ir = Compiler.compile(parse.program, parse.diagnostics);
    const context = new EvalContext();
    context.charCodeEncodingName = load.encodingName;
    const vm = new VirtualMachine(ir, context);
    const pickedCaptions: string[] = [];

    vm.runUntilWaitOrHalt(scenario.maxInstructionsPerRun ?? 10_000);
    assertCheckpoint(vm, scenario.initialCheckpoint);

    for (const step of scenario.steps ?? []) {
      if (vm.status !== vmStatus.waitingForChoice) {
        throw new Error(`Expected VM status WaitingForChoice before pick, got ${vm.status}.`);
      }

      const button = resolveButton(vm, step.pick);
      pickedCaptions.push(button.caption);
      if (!vm.chooseButton(button.id)) {
        throw new Error(`Failed to choose button '${button.caption}' (${button.id}).`);
      }

      vm.runUntilWaitOrHalt(scenario.maxInstructionsPerRun ?? 10_000);
      assertCheckpoint(vm, step.checkpoint);
    }

    assertCheckpoint(vm, scenario.finalCheckpoint);
    return {
      vm,
      detectedEncodingName: load.encodingName,
      pickedCaptions
    };
  }

  static loadScenariosFromJson(jsonPath: string): QuestScenario[] {
    const json = readFileSync(jsonPath, "utf-8");
    const root = JSON.parse(json) as {
      scenarios?: Array<{
        name?: string;
        scriptPath?: string;
        encodingName?: string;
        maxInstructionsPerRun?: number;
        initialCheckpoint?: QuestCheckpoint;
        steps?: QuestScenarioStep[];
        finalCheckpoint?: QuestCheckpoint;
      }>;
    };

    return (root.scenarios ?? []).map((scenario) => {
      if (!scenario.scriptPath?.trim()) {
        throw new Error(`Scenario '${scenario.name ?? "<unnamed>"}' has empty scriptPath.`);
      }

      return {
        name: scenario.name,
        scriptPath: resolveFromRepo(scenario.scriptPath),
        encodingName: scenario.encodingName?.trim() || "auto",
        maxInstructionsPerRun:
          scenario.maxInstructionsPerRun && scenario.maxInstructionsPerRun > 0
            ? scenario.maxInstructionsPerRun
            : 10_000,
        initialCheckpoint: scenario.initialCheckpoint,
        steps: scenario.steps ?? [],
        finalCheckpoint: scenario.finalCheckpoint
      };
    });
  }
}

export function resolveFromRepo(relativePath: string): string {
  let current = dirname(fileURLToPath(import.meta.url));

  while (true) {
    const candidate = join(current, "FireURQ3.sln");
    try {
      readFileSync(candidate);
      return join(current, relativePath);
    } catch {
      const parent = dirname(current);
      if (parent === current) {
        throw new Error("Failed to locate repository root (FireURQ3.sln).");
      }

      current = parent;
    }
  }
}

function resolveButton(vm: VirtualMachine, pick: ButtonPick): ButtonAction {
  if (pick.index !== undefined) {
    if (pick.index < 0 || pick.index >= vm.buttons.length) {
      throw new Error(`Button index ${pick.index} is out of range. Buttons count: ${vm.buttons.length}.`);
    }

    return vm.buttons[pick.index]!;
  }

  if (pick.caption?.trim()) {
    const byCaption = vm.buttons.find(
      (button) => button.caption.toLowerCase() === pick.caption!.toLowerCase()
    );
    if (byCaption) {
      return byCaption;
    }

    const normalizedPick = normalizeCaptionForMatch(pick.caption);
    const normalized = vm.buttons.find(
      (button) => normalizeCaptionForMatch(button.caption).toLowerCase() === normalizedPick.toLowerCase()
    );
    if (normalized) {
      return normalized;
    }

    throw new Error(
      `Button with caption '${pick.caption}' was not found. Available: ${vm.buttons.map((button) => button.caption).join(", ")}.`
    );
  }

  throw new Error("Button pick must define either caption or index.");
}

function assertCheckpoint(vm: VirtualMachine, checkpoint?: QuestCheckpoint): void {
  if (!checkpoint) {
    return;
  }

  if (checkpoint.status !== undefined) {
    expect(vm.status).toBe(checkpoint.status);
  }

  for (const [name, value] of Object.entries(checkpoint.numberVariables ?? {})) {
    const variable = vm.context.variables.tryGet(name);
    expect(variable, `Expected variable '${name}' to exist.`).not.toBeNull();
    expect(variable?.numberValue).toBe(value);
  }

  for (const [name, value] of Object.entries(checkpoint.stringVariables ?? {})) {
    const variable = vm.context.variables.tryGet(name);
    expect(variable, `Expected variable '${name}' to exist.`).not.toBeNull();
    expect(variable?.stringValue).toBe(value);
  }

  for (const [name, value] of Object.entries(checkpoint.inventory ?? {})) {
    expect(vm.context.inventory.getCount(name)).toBe(value);
  }

  for (const fragment of checkpoint.outputContains ?? []) {
    expect(vm.outputText).toContain(fragment);
  }

  if (checkpoint.hasErrorDiagnostics !== undefined) {
    const actualHasErrors = vm.context.diagnostics.items.some((item) => item.severity === "error");
    expect(actualHasErrors).toBe(checkpoint.hasErrorDiagnostics);
  }
}

function normalizeCaptionForMatch(caption: string): string {
  return caption.trim().replace(/\.+$/u, "");
}
