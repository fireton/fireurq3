import { describe, expect, it } from "vitest";
import { vmStatus } from "../src/vm.js";
import { QuestScenarioHarness, resolveFromRepo } from "./scenario-harness.js";

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
});
