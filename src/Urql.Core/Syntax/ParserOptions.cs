namespace Urql.Core.Syntax;

public sealed record ParserOptions(
    CompatibilityMode CompatibilityMode = CompatibilityMode.DosUrq)
{
    public bool AllowUnknownCommands => true;
}
