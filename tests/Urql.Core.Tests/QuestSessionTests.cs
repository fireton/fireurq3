using Urql.Core.Runtime;
using Urql.Runner.MonoGame.Runner;

namespace Urql.Core.Tests;

public sealed class QuestSessionTests
{
    [Fact]
    public void Advance_ShouldExposeChoicesAfterTranscriptOutput()
    {
        var scriptPath = WriteTempQuest(
            ":start\n" +
            "pln Привет\n" +
            "btn next,Выйти\n" +
            "end\n" +
            ":next\n" +
            "pln Пока\n" +
            "end\n");

        var session = new QuestSession();
        session.Load(scriptPath, new QuestRunnerConfig());

        var snapshot = session.Advance();

        Assert.Equal(VmStatus.WaitingForChoice, snapshot.Status);
        Assert.Single(snapshot.ActiveChoices);
        Assert.Contains(snapshot.Transcript, t => t.Kind == TranscriptEntryKind.Output && t.Text.Contains("Привет", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectButton_ShouldAppendBracketedEcho_AndClearChoices()
    {
        var scriptPath = WriteTempQuest(
            ":start\n" +
            "pln Тест\n" +
            "btn next,Выйти\n" +
            "end\n" +
            ":next\n" +
            "pln Пока\n" +
            "end\n");

        var session = new QuestSession();
        session.Load(scriptPath, new QuestRunnerConfig());
        var beforeChoice = session.Advance();

        var pickedButton = Assert.Single(beforeChoice.ActiveChoices);
        var afterChoice = session.SelectButton(pickedButton.Id);

        Assert.Contains(afterChoice.Transcript, t =>
            t.Kind == TranscriptEntryKind.ChoiceEcho &&
            string.Equals(t.Text, "[Выйти]", StringComparison.Ordinal));
        Assert.Empty(afterChoice.ActiveChoices);
        Assert.Equal(VmStatus.Halted, afterChoice.Status);
        Assert.Contains(afterChoice.Transcript, t => t.Kind == TranscriptEntryKind.Output && t.Text.Contains("Пока", StringComparison.Ordinal));
    }

    [Fact]
    public void Advance_ShouldSurfaceInstructionLimit()
    {
        var scriptPath = WriteTempQuest(
            ":start\n" +
            "pln loop\n" +
            "goto start\n" +
            "end\n");

        var session = new QuestSession();
        session.Load(scriptPath, new QuestRunnerConfig(MaxInstructionsPerAdvance: 5));

        var snapshot = session.Advance();

        Assert.True(snapshot.HitInstructionLimit);
        Assert.Equal(VmStatus.Running, snapshot.Status);
    }

    private static string WriteTempQuest(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"quest-session-{Guid.NewGuid():N}.qst");
        File.WriteAllText(path, content);
        return path;
    }
}
