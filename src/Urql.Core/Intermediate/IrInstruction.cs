using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Intermediate;

public abstract record IrInstruction(SourceSpan Span);

public sealed record AssignInstruction(
    string Name,
    ExpressionSyntax Expression,
    bool ForceString,
    SourceSpan Span) : IrInstruction(Span);

public sealed record JumpInstruction(
    int TargetIndex,
    SourceSpan Span) : IrInstruction(Span);

public sealed record JumpDynamicInstruction(
    ExpressionSyntax TargetExpression,
    SourceSpan Span) : IrInstruction(Span);

public sealed record JumpIfFalseInstruction(
    ExpressionSyntax ConditionExpression,
    int TargetIndex,
    SourceSpan Span) : IrInstruction(Span);

public sealed record CallInstruction(
    int TargetIndex,
    SourceSpan Span) : IrInstruction(Span);

public sealed record CallDynamicInstruction(
    ExpressionSyntax TargetExpression,
    SourceSpan Span) : IrInstruction(Span);

public sealed record ReturnOrHaltInstruction(SourceSpan Span) : IrInstruction(Span);

public sealed record PrintInstruction(
    ExpressionSyntax TextExpression,
    bool AppendNewline,
    SourceSpan Span) : IrInstruction(Span);

public sealed record AddButtonInstruction(
    ExpressionSyntax TargetExpression,
    ExpressionSyntax CaptionExpression,
    SourceSpan Span) : IrInstruction(Span);

public sealed record PerkillInstruction(SourceSpan Span) : IrInstruction(Span);

public sealed record InvkillInstruction(
    ExpressionSyntax? ItemExpression,
    SourceSpan Span) : IrInstruction(Span);

public sealed record InvAddInstruction(
    ExpressionSyntax? CountExpression,
    ExpressionSyntax ItemExpression,
    SourceSpan Span) : IrInstruction(Span);

public sealed record InvRemoveInstruction(
    ExpressionSyntax? CountExpression,
    ExpressionSyntax ItemExpression,
    SourceSpan Span) : IrInstruction(Span);

public sealed record NoOpInstruction(SourceSpan Span) : IrInstruction(Span);
