using Urql.Core.Diagnostics;

namespace Urql.Core.Syntax;

public sealed record LexResult(
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<Diagnostic> Diagnostics);
