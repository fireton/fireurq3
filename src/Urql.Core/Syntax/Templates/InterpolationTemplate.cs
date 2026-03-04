namespace Urql.Core.Syntax.Templates;

public sealed record InterpolationTemplate(IReadOnlyList<TemplatePart> Parts);

public abstract record TemplatePart;

public sealed record LiteralTextPart(string Text) : TemplatePart;

public sealed record EmbeddedExpressionPart(
    InterpolationTemplate Content,
    SourceSpan Span) : TemplatePart;
