using Urql.Core.Runtime;
using Urql.Core.Diagnostics;

namespace Urql.Core.Tests;

public sealed class InterpolationExpanderTests
{
    [Fact]
    public void ExpandInterpolations_ShouldExpandSimpleExpression()
    {
        var ctx = new EvalContext();
        var result = InterpolationExpander.ExpandInterpolations("x=#1+2$", ctx);

        Assert.Equal("x=3", result);
        Assert.Empty(ctx.Diagnostics.Items.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void ExpandInterpolations_ShouldExpandNestedInnerToOuter()
    {
        var ctx = new EvalContext();
        ctx.Variables.Set("a", UrqlValue.Number(5));
        ctx.Variables.Set("b", UrqlValue.Number(2));

        var result = InterpolationExpander.ExpandInterpolations("v=#a+#b$$", ctx);

        Assert.Equal("v=7", result);
        Assert.Empty(ctx.Diagnostics.Items.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void ExpandInterpolations_ShouldWorkInsideQuotes()
    {
        var ctx = new EvalContext();
        ctx.Variables.Set("name", UrqlValue.String("world"));

        var result = InterpolationExpander.ExpandInterpolations("p \"hello #%name$\"", ctx);

        Assert.Equal("p \"hello world\"", result);
    }

    [Fact]
    public void ExpandInterpolations_ShouldSupportLegacyStringInterpolationPercentPrefix()
    {
        var ctx = new EvalContext();
        ctx.Variables.Set("name", UrqlValue.String("Korwin"));
        ctx.Variables.Set("money", UrqlValue.Number(100));

        var result = InterpolationExpander.ExpandInterpolations("n=#%name$,m=#%money$", ctx);

        Assert.Equal("n=Korwin,m=", result);
    }

    [Fact]
    public void ExpandInterpolations_ShouldKeepNumericPathForNonPercentInterpolation()
    {
        var ctx = new EvalContext();
        ctx.Variables.Set("name", UrqlValue.String("abcd"));

        var result = InterpolationExpander.ExpandInterpolations("len=#name$", ctx);

        Assert.Equal("len=4", result);
    }

    [Fact]
    public void ExpandInterpolations_ShouldReportUnmatchedCloseDelimiter()
    {
        var ctx = new EvalContext();

        _ = InterpolationExpander.ExpandInterpolations("x=1$", ctx);

        Assert.Contains(ctx.Diagnostics.Items, d => d.Message.Contains("Unmatched interpolation close delimiter"));
    }

    [Fact]
    public void ExpandInterpolations_ShouldReportUnmatchedOpenDelimiter()
    {
        var ctx = new EvalContext();

        _ = InterpolationExpander.ExpandInterpolations("x=#1+2", ctx);

        Assert.Contains(ctx.Diagnostics.Items, d => d.Message.Contains("Unmatched interpolation open delimiter"));
    }

    [Fact]
    public void ExpandInterpolations_ShouldSubstituteEmptyOnBadExpression()
    {
        var ctx = new EvalContext();

        var result = InterpolationExpander.ExpandInterpolations("x=#(1+$", ctx);

        Assert.Equal("x=", result);
        Assert.Contains(ctx.Diagnostics.Items, d => d.Message.Contains("Failed to parse interpolation expression"));
    }
}
