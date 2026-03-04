using Urql.Core.Diagnostics;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Syntax;

public sealed record ExpressionParseResult(
    ExpressionSyntax Expression,
    IReadOnlyList<Diagnostic> Diagnostics);
