using Urql.Core.Diagnostics;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Syntax;

public sealed record ParseResult(
    ProgramSyntax Program,
    IReadOnlyList<Diagnostic> Diagnostics);
