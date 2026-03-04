using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Runtime;

public static class ExpressionEvaluator
{
    public static UrqlValue Evaluate(ExpressionSyntax expression, EvalContext context)
    {
        return expression switch
        {
            NumberLiteralExpressionSyntax number => UrqlValue.Number(number.Value),
            StringLiteralExpressionSyntax str => UrqlValue.String(str.Value),
            IdentifierExpressionSyntax id => ResolveIdentifier(id.Name, context),
            ParenthesizedExpressionSyntax p => Evaluate(p.Inner, context),
            RawTextExpressionSyntax raw => UrqlValue.String(raw.RawText),
            UnaryExpressionSyntax unary => EvaluateUnary(unary, context),
            BinaryExpressionSyntax binary => EvaluateBinary(binary, context),
            _ => UrqlValue.Number(0d)
        };
    }

    private static UrqlValue ResolveIdentifier(string name, EvalContext context)
    {
        if (name.StartsWith("inv_", StringComparison.OrdinalIgnoreCase))
        {
            var item = name[4..];
            return UrqlValue.Number(context.Inventory.GetCount(item));
        }

        if (context.Variables.TryGet(name, out var value))
        {
            return value;
        }

        var inventoryCount = context.Inventory.GetCount(name);
        if (inventoryCount > 0d)
        {
            return UrqlValue.Number(inventoryCount);
        }

        return UrqlValue.Number(0d);
    }

    private static UrqlValue EvaluateUnary(UnaryExpressionSyntax unary, EvalContext context)
    {
        var operand = Evaluate(unary.Operand, context);

        return unary.Operator switch
        {
            TokenKind.Plus => UrqlValue.Number(context.ToNumber(operand)),
            TokenKind.Minus => UrqlValue.Number(-context.ToNumber(operand)),
            TokenKind.KeywordNot => UrqlValue.Bool(!context.ToBool(operand)),
            _ => UrqlValue.Number(0d)
        };
    }

    private static UrqlValue EvaluateBinary(BinaryExpressionSyntax binary, EvalContext context)
    {
        var left = Evaluate(binary.Left, context);
        var right = Evaluate(binary.Right, context);

        return binary.Operator switch
        {
            TokenKind.Plus => EvaluatePlus(left, right, context),
            TokenKind.Minus => UrqlValue.Number(context.ToNumber(left) - context.ToNumber(right)),
            TokenKind.Star => UrqlValue.Number(context.ToNumber(left) * context.ToNumber(right)),
            TokenKind.Slash => UrqlValue.Number(context.ToNumber(left) / context.ToNumber(right)),
            TokenKind.Percent => UrqlValue.Number(context.ToNumber(left) % context.ToNumber(right)),
            TokenKind.Equals => UrqlValue.Bool(Eq(left, right, context)),
            TokenKind.NotEquals => UrqlValue.Bool(!Eq(left, right, context)),
            TokenKind.DoubleEquals => UrqlValue.Bool(WildcardMatch(
                context.ToUrqlString(left),
                context.ToUrqlString(right))),
            TokenKind.Less => UrqlValue.Bool(context.ToNumber(left) < context.ToNumber(right)),
            TokenKind.LessOrEquals => UrqlValue.Bool(context.ToNumber(left) <= context.ToNumber(right)),
            TokenKind.Greater => UrqlValue.Bool(context.ToNumber(left) > context.ToNumber(right)),
            TokenKind.GreaterOrEquals => UrqlValue.Bool(context.ToNumber(left) >= context.ToNumber(right)),
            TokenKind.KeywordAnd => UrqlValue.Bool(context.ToBool(left) && context.ToBool(right)),
            TokenKind.KeywordOr => UrqlValue.Bool(context.ToBool(left) || context.ToBool(right)),
            _ => UrqlValue.Number(0d)
        };
    }

    private static UrqlValue EvaluatePlus(UrqlValue left, UrqlValue right, EvalContext context)
    {
        if (left.Kind == ValueKind.String)
        {
            return UrqlValue.String(context.ToUrqlString(left) + context.ToUrqlString(right));
        }

        return UrqlValue.Number(context.ToNumber(left) + context.ToNumber(right));
    }

    private static bool Eq(UrqlValue left, UrqlValue right, EvalContext context)
    {
        if (left.Kind == ValueKind.String || right.Kind == ValueKind.String)
        {
            return string.Equals(context.ToUrqlString(left), context.ToUrqlString(right), StringComparison.OrdinalIgnoreCase);
        }

        return Math.Abs(context.ToNumber(left) - context.ToNumber(right)) < 1e-12;
    }

    private static bool WildcardMatch(string input, string pattern)
    {
        return WildcardMatchCore(input ?? string.Empty, pattern ?? string.Empty, ignoreCase: true);
    }

    private static bool WildcardMatchCore(string input, string pattern, bool ignoreCase)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var i = 0;
        var p = 0;
        var star = -1;
        var match = 0;

        while (i < input.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || string.Equals(input[i].ToString(), pattern[p].ToString(), comparison)))
            {
                i++;
                p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                match = i;
            }
            else if (star != -1)
            {
                p = star + 1;
                i = ++match;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
