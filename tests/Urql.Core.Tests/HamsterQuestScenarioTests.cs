using Urql.Core.Runtime;

namespace Urql.Core.Tests;

public sealed class HamsterQuestScenarioTests
{
    [Fact]
    public void Hamster1_Scenarios_ShouldRunWithoutFaultAndWithoutErrorDiagnostics()
    {
        var scenarioFile = TestPaths.ResolveFromRepo("tests/quests/hamster1.walk.json");
        var scenarios = QuestScenarioHarness.LoadScenariosFromJson(scenarioFile);

        Assert.True(scenarios.Count >= 2, "Expected at least two scenarios for hamster1.");
        foreach (var scenario in scenarios)
        {
            var result = QuestScenarioHarness.Run(scenario);
            Assert.NotEqual(VmStatus.Faulted, result.Vm.Status);
            Assert.Contains(result.Vm.Status, [VmStatus.WaitingForChoice, VmStatus.Halted]);
        }
    }

    [Fact]
    public void Hamster2_Scenarios_ShouldRunWithoutFaultAndWithoutErrorDiagnostics()
    {
        var scenarioFile = TestPaths.ResolveFromRepo("tests/quests/hamster2.walk.json");
        var scenarios = QuestScenarioHarness.LoadScenariosFromJson(scenarioFile);

        Assert.True(scenarios.Count >= 2, "Expected at least two scenarios for hamster2.");
        foreach (var scenario in scenarios)
        {
            var result = QuestScenarioHarness.Run(scenario);
            Assert.NotEqual(VmStatus.Faulted, result.Vm.Status);
            Assert.Contains(result.Vm.Status, [VmStatus.WaitingForChoice, VmStatus.Halted]);
        }
    }

    [Fact]
    public void Hamster1_ShouldReachWinningFanfareEnding()
    {
        var scenarioFile = TestPaths.ResolveFromRepo("tests/quests/hamster1.walk.json");
        var scenarios = QuestScenarioHarness.LoadScenariosFromJson(scenarioFile);
        var winning = scenarios.Single(s => string.Equals(s.Name, "hamster1_win_fanfare_escape", StringComparison.Ordinal));

        var result = QuestScenarioHarness.Run(winning);

        Assert.Equal(VmStatus.Halted, result.Vm.Status);
        Assert.Contains("под барабанную дробь и звуки фанфар выбираетесь из клетки", result.Vm.OutputText);
    }

    [Fact]
    public void Hamster2_ShouldReachWinningFanfareEnding()
    {
        var scenarioFile = TestPaths.ResolveFromRepo("tests/quests/hamster2.walk.json");
        var scenarios = QuestScenarioHarness.LoadScenariosFromJson(scenarioFile);
        var winning = scenarios.Single(s => string.Equals(s.Name, "hamster2_win_fanfare_descent", StringComparison.Ordinal));

        var result = QuestScenarioHarness.Run(winning);

        Assert.Equal(VmStatus.Halted, result.Vm.Status);
        Assert.Contains("под звуки фанфар спускаетесь по ней", result.Vm.OutputText);
    }
}
