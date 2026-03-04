using Urql.Core.Runtime;

namespace Urql.Core.Tests;

public sealed class QuestScenarioHarnessTests
{
    [Fact]
    public void ScenarioHarness_ShouldWalkByCaption_AndAssertState()
    {
        var scenario = new QuestScenario(
            ScriptPath: TestPaths.ResolveFromRepo("tests/quests/harness_sample.qst"),
            InitialCheckpoint: new QuestCheckpoint(
                Status: VmStatus.WaitingForChoice,
                OutputContains: ["Start"]),
            Steps:
            [
                new QuestScenarioStep(
                    new ButtonPick(Caption: "Left"),
                    new QuestCheckpoint(
                        Status: VmStatus.WaitingForChoice,
                        StringVariables: new Dictionary<string, string> { ["route"] = "left" },
                        Inventory: new Dictionary<string, double> { ["key"] = 1 },
                        OutputContains: ["Took left"])),
                new QuestScenarioStep(
                    new ButtonPick(Caption: "Finish"))
            ],
            FinalCheckpoint: new QuestCheckpoint(
                Status: VmStatus.Halted,
                OutputContains: ["Done"],
                HasErrorDiagnostics: false));

        var result = QuestScenarioHarness.Run(scenario);

        Assert.Equal("utf-8", result.DetectedEncodingName);
        Assert.Equal(["Left", "Finish"], result.PickedCaptions);
    }

    [Fact]
    public void ScenarioHarness_ShouldWalkByIndex()
    {
        var scenario = new QuestScenario(
            ScriptPath: TestPaths.ResolveFromRepo("tests/quests/harness_sample.qst"),
            Steps:
            [
                new QuestScenarioStep(new ButtonPick(Index: 1)),
                new QuestScenarioStep(new ButtonPick(Index: 0))
            ],
            FinalCheckpoint: new QuestCheckpoint(
                Status: VmStatus.Halted,
                StringVariables: new Dictionary<string, string> { ["route"] = "right" },
                OutputContains: ["Took right", "Done"],
                HasErrorDiagnostics: false));

        var result = QuestScenarioHarness.Run(scenario);

        Assert.Equal(["Right", "Finish"], result.PickedCaptions);
    }
}

