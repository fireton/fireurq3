namespace Urql.Core.Syntax.Ast;

public sealed record ProgramSyntax(
    IReadOnlyList<StatementLineSyntax> Lines,
    SourceSpan Span) : SyntaxNode(Span);
