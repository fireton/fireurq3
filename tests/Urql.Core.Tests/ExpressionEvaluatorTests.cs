using Urql.Core.Runtime;
using Urql.Core.Diagnostics;
using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Tests;

public sealed class ExpressionEvaluatorTests
{
    [Theory]
    [InlineData("1+2*3", 7d)]
    [InlineData("(1+2)*3", 9d)]
    [InlineData("10/2+1", 6d)]
    public void Evaluate_ShouldHandleArithmeticPrecedence(string exprText, double expected)
    {
        var expr = ParseExpr(exprText);
        var ctx = new EvalContext();

        var value = ExpressionEvaluator.Evaluate(expr, ctx);

        Assert.Equal(ValueKind.Number, value.Kind);
        Assert.Equal(expected, value.NumberValue, 6);
    }

    [Fact]
    public void Evaluate_ShouldHandleBooleanAndComparisons()
    {
        var expr = ParseExpr("1<2 and not 0");
        var ctx = new EvalContext();

        var value = ExpressionEvaluator.Evaluate(expr, ctx);

        Assert.Equal(ValueKind.Bool, value.Kind);
        Assert.True(value.BoolValue);
    }

    [Fact]
    public void Evaluate_ShouldResolveVariablesCaseInsensitive()
    {
        var expr = ParseExpr("Money+2");
        var ctx = new EvalContext();
        ctx.Variables.Set("money", UrqlValue.Number(5));

        var value = ExpressionEvaluator.Evaluate(expr, ctx);

        Assert.Equal(7d, value.NumberValue, 6);
    }

    [Fact]
    public void Evaluate_ShouldSupportWildcardDoubleEquals()
    {
        var expr = ParseExpr("\"пароль\"==\"*рол*\"");
        var ctx = new EvalContext();
        var value = ExpressionEvaluator.Evaluate(expr, ctx);

        Assert.Equal(ValueKind.Bool, value.Kind);
        Assert.True(value.BoolValue);
    }

    private static ExpressionSyntax ParseExpr(string text)
    {
        var parsed = Parser.ParseExpressionText(text);
        Assert.DoesNotContain(parsed.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        return parsed.Expression;
    }
}
