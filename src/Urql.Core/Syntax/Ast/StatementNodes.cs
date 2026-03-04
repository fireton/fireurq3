namespace Urql.Core.Syntax.Ast;

public sealed record AssignmentStatementSyntax(
    string Name,
    ExpressionSyntax Expression,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record InstrStatementSyntax(
    string Name,
    ExpressionSyntax Expression,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record IfStatementSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax> ThenStatements,
    IReadOnlyList<StatementSyntax>? ElseStatements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record GotoStatementSyntax(
    ExpressionSyntax Target,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record ProcStatementSyntax(
    ExpressionSyntax Target,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record EndStatementSyntax(SourceSpan Span) : StatementSyntax(Span);

public sealed record PrintStatementSyntax(
    ExpressionSyntax TextExpression,
    bool AppendNewLine,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record BtnStatementSyntax(
    ExpressionSyntax TargetExpression,
    ExpressionSyntax CaptionExpression,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record PerkillStatementSyntax(SourceSpan Span) : StatementSyntax(Span);

public sealed record InvkillStatementSyntax(
    ExpressionSyntax? ItemExpression,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record InvAddStatementSyntax(
    ExpressionSyntax? CountExpression,
    ExpressionSyntax ItemExpression,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record InvRemoveStatementSyntax(
    ExpressionSyntax? CountExpression,
    ExpressionSyntax ItemExpression,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record InvalidStatementSyntax(
    string Reason,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record UnknownCommandStatementSyntax(
    string CommandName,
    ExpressionSyntax? Arguments,
    SourceSpan Span) : StatementSyntax(Span);
