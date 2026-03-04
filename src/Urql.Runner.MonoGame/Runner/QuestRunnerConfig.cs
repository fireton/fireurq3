namespace Urql.Runner.MonoGame.Runner;

public sealed record QuestRunnerConfig(
    int MaxInstructionsPerAdvance = 10_000,
    bool StrictParserMode = false,
    string EncodingName = "auto");
