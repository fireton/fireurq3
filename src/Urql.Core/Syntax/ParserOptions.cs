namespace Urql.Core.Syntax;

public sealed record ParserOptions(
    CompatibilityMode CompatibilityMode = CompatibilityMode.DosUrq,
    bool AllowUnknownCommands = true);
