import { describe, expect, it } from "vitest";
import { Compiler } from "../src/compiler.js";
import { Parser } from "../src/parser.js";
import { UrqlTextLoader } from "../src/io.js";
import { vmStatus } from "../src/vm.js";
import { QuestScenarioHarness, resolveFromRepo } from "./scenario-harness.js";
import { readFileSync } from "node:fs";

describe("Hamster scenarios", () => {
  it("hamster1 scenarios run without fault", () => {
    const scenarios = QuestScenarioHarness.loadScenariosFromJson(
      resolveFromRepo("tests/quests/hamster1.walk.json")
    );

    expect(scenarios.length).toBeGreaterThanOrEqual(2);
    for (const scenario of scenarios) {
      const result = QuestScenarioHarness.run(scenario);
      expect(result.vm.status).not.toBe(vmStatus.faulted);
      expect([vmStatus.waitingForChoice, vmStatus.halted]).toContain(result.vm.status);
    }
  });

  it("hamster2 scenarios run without fault", () => {
    const scenarios = QuestScenarioHarness.loadScenariosFromJson(
      resolveFromRepo("tests/quests/hamster2.walk.json")
    );

    expect(scenarios.length).toBeGreaterThanOrEqual(2);
    for (const scenario of scenarios) {
      const result = QuestScenarioHarness.run(scenario);
      expect(result.vm.status).not.toBe(vmStatus.faulted);
      expect([vmStatus.waitingForChoice, vmStatus.halted]).toContain(result.vm.status);
    }
  });

  it("hamster1 reaches winning fanfare ending", () => {
    const scenarios = QuestScenarioHarness.loadScenariosFromJson(
      resolveFromRepo("tests/quests/hamster1.walk.json")
    );
    const winning = scenarios.find((item) => item.name === "hamster1_win_fanfare_escape");
    expect(winning).toBeDefined();

    const result = QuestScenarioHarness.run(winning!);

    expect(result.vm.status).toBe(vmStatus.halted);
    expect(result.vm.outputText).toContain(
      "под барабанную дробь и звуки фанфар выбираетесь из клетки"
    );
  });

  it("hamster2 reaches winning fanfare ending", () => {
    const scenarios = QuestScenarioHarness.loadScenariosFromJson(
      resolveFromRepo("tests/quests/hamster2.walk.json")
    );
    const winning = scenarios.find((item) => item.name === "hamster2_win_fanfare_descent");
    expect(winning).toBeDefined();

    const result = QuestScenarioHarness.run(winning!);

    expect(result.vm.status).toBe(vmStatus.halted);
    expect(result.vm.outputText).toContain("под звуки фанфар спускаетесь по ней");
  });

  it("hamster2 parses email text without diagnostics", () => {
    const bytes = new Uint8Array(readFileSync(resolveFromRepo("tests/quests/hamster2.qst")));
    const load = UrqlTextLoader.decode(bytes, { encodingName: "auto" });
    const parse = Parser.parse(load.text);
    const ir = Compiler.compile(parse.program, parse.diagnostics);

    expect(load.encodingName).toBe("cp1251");
    expect(parse.diagnostics).toHaveLength(0);
    expect(ir.diagnostics).toHaveLength(0);
  });
});
