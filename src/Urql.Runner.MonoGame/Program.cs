using Urql.Runner.MonoGame;
using Urql.Runner.MonoGame.Runtime;

var questPath = args.FirstOrDefault();
if (string.IsNullOrWhiteSpace(questPath))
{
    questPath = QuestFilePicker.TryPickQuestFilePath();
}

if (string.IsNullOrWhiteSpace(questPath))
{
    return;
}

using var game = new RunnerGame(questPath);
game.Run();
