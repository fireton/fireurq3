import { describe, expect, it } from "vitest";
import { vmStatus } from "../src/vm.js";
import {
  QuestScenarioHarness,
  resolveFromRepo,
  type QuestScenario
} from "./scenario-harness.js";

describe("QuestScenarioHarness", () => {
  it("walks by caption and asserts state", () => {
    const scenario: QuestScenario = {
      scriptPath: resolveFromRepo("tests/quests/harness_sample.qst"),
      initialCheckpoint: {
        status: vmStatus.waitingForChoice,
        outputContains: ["Start"]
      },
      steps: [
        {
          pick: { caption: "Left" },
          checkpoint: {
            status: vmStatus.waitingForChoice,
            stringVariables: { route: "left" },
            inventory: { key: 1 },
            outputContains: ["Took left"]
          }
        },
        {
          pick: { caption: "Finish" }
        }
      ],
      finalCheckpoint: {
        status: vmStatus.halted,
        outputContains: ["Done"],
        hasErrorDiagnostics: false
      }
    };

    const result = QuestScenarioHarness.run(scenario);

    expect(result.detectedEncodingName).toBe("utf-8");
    expect(result.pickedCaptions).toEqual(["Left", "Finish"]);
  });

  it("walks by index", () => {
    const scenario: QuestScenario = {
      scriptPath: resolveFromRepo("tests/quests/harness_sample.qst"),
      steps: [{ pick: { index: 1 } }, { pick: { index: 0 } }],
      finalCheckpoint: {
        status: vmStatus.halted,
        stringVariables: { route: "right" },
        outputContains: ["Took right", "Done"],
        hasErrorDiagnostics: false
      }
    };

    const result = QuestScenarioHarness.run(scenario);

    expect(result.pickedCaptions).toEqual(["Right", "Finish"]);
  });
});
