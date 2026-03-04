using System.Text;
using Urql.Core.Diagnostics;
using Urql.Core.Syntax;
using Urql.Core.Syntax.Templates;

namespace Urql.Core.Runtime;

public static class InterpolationExpander
{
    public static string ExpandInterpolations(string input, EvalContext ctx)
    {
        var current = input ?? string.Empty;

        for (var pass = 0; pass < 16; pass++)
        {
            var template = InterpolationTemplateParser.Parse(current, ctx.Diagnostics, out var hadInterpolation);
            if (!hadInterpolation)
            {
                return current;
            }

            var next = RenderTemplate(template, ctx);
            if (next == current)
            {
                return next;
            }

            current = next;
        }

        ctx.Diagnostics.Report(
            DiagnosticCode.UnexpectedCharacter,
            DiagnosticSeverity.Warning,
            "Interpolation expansion reached maximum pass count (16).",
            new SourceSpan(new SourcePosition(1, 1), new SourcePosition(1, 1)));
        return current;
    }

    private static string RenderTemplate(InterpolationTemplate template, EvalContext ctx)
    {
        var output = new StringBuilder();

        foreach (var part in template.Parts)
        {
            switch (part)
            {
                case LiteralTextPart literal:
                    output.Append(literal.Text);
                    break;

                case EmbeddedExpressionPart embedded:
                {
                    var expressionText = RenderTemplate(embedded.Content, ctx);
                    if (TryRenderLegacyStringInterpolation(expressionText, ctx, out var stringValue))
                    {
                        output.Append(stringValue);
                        break;
                    }

                    var expressionParse = Parser.ParseExpressionText(expressionText);
                    if (expressionParse.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        ctx.Diagnostics.Report(
                            DiagnosticCode.UnexpectedCharacter,
                            DiagnosticSeverity.Error,
                            $"Failed to parse interpolation expression '{expressionText}'.",
                            embedded.Span);
                        continue;
                    }

                    var value = ExpressionEvaluator.Evaluate(expressionParse.Expression, ctx);
                    output.Append(FormatNumericInterpolation(value, ctx));
                    break;
                }
            }
        }

        return output.ToString();
    }

    private static bool TryRenderLegacyStringInterpolation(string expressionText, EvalContext ctx, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return false;
        }

        if (expressionText[0] != '%')
        {
            return false;
        }

        var variableName = expressionText[1..].Trim();
        if (variableName.Length == 0)
        {
            return false;
        }

        if (!IsIdentifier(variableName))
        {
            return false;
        }

        var v = ctx.Variables.GetOrDefault(variableName);
        value = ctx.ToUrqlString(v);
        return true;
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (!(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatNumericInterpolation(UrqlValue value, EvalContext ctx)
    {
        var numeric = value.Kind switch
        {
            ValueKind.Bool => value.BoolValue ? 1d : 0d,
            _ => ctx.ToNumber(value)
        };

        return numeric.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
    }
}
