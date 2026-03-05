namespace Urql.Player.Compat.Session;

public sealed record PlayerSessionConfig(
    string QuestPath,
    string EncodingName = "auto",
    bool StrictUnknownCommands = false,
    CompatibilityProfile CompatibilityProfile = CompatibilityProfile.FireUrqLegacy,
    string? SkinPathOverride = null,
    int VirtualWidth = 800,
    int VirtualHeight = 600,
    int MaxInstructionsPerAdvance = 10_000);
