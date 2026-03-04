namespace Urql.Core.Syntax.Ast;

public abstract record StatementSyntax(SourceSpan Span) : SyntaxNode(Span);
