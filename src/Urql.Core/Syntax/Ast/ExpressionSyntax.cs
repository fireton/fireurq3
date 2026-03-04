namespace Urql.Core.Syntax.Ast;

public abstract record ExpressionSyntax(SourceSpan Span) : SyntaxNode(Span);

public sealed record NumberLiteralExpressionSyntax(
    double Value,
    string RawText,
    SourceSpan Span) : ExpressionSyntax(Span);

public sealed record StringLiteralExpressionSyntax(
    string Value,
    string RawText,
    SourceSpan Span) : ExpressionSyntax(Span);

public sealed record IdentifierExpressionSyntax(
    string Name,
    SourceSpan Span) : ExpressionSyntax(Span);

public sealed record UnaryExpressionSyntax(
    TokenKind Operator,
    ExpressionSyntax Operand,
    SourceSpan Span) : ExpressionSyntax(Span);

public sealed record BinaryExpressionSyntax(
    ExpressionSyntax Left,
    TokenKind Operator,
    ExpressionSyntax Right,
    SourceSpan Span) : ExpressionSyntax(Span);

public sealed record ParenthesizedExpressionSyntax(
    ExpressionSyntax Inner,
    SourceSpan Span) : ExpressionSyntax(Span);

public sealed record RawTextExpressionSyntax(
    string RawText,
    SourceSpan Span) : ExpressionSyntax(Span);
