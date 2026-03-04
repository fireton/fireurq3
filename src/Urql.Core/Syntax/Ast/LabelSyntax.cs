namespace Urql.Core.Syntax.Ast;

public sealed record LabelSyntax(
    string Name,
    SourceSpan Span) : SyntaxNode(Span);
