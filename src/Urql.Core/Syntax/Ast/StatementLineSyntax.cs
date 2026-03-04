namespace Urql.Core.Syntax.Ast;

public sealed record StatementLineSyntax(
    LabelSyntax? Label,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : SyntaxNode(Span);
