namespace Urql.Core.Syntax;

public sealed record Token(
    TokenKind Kind,
    string Text,
    SourceSpan Span,
    object? Value = null);
