using System.Globalization;
using Urql.Core.Diagnostics;

namespace Urql.Core.Runtime;

public sealed class EvalContext
{
    public VariableStore Variables { get; } = new();
    public InventoryStore Inventory { get; } = new();
    public DiagnosticBag Diagnostics { get; } = new();

    public double ToNumber(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.Number => value.NumberValue,
            ValueKind.String => value.StringValue.Length,
            ValueKind.Bool => value.BoolValue ? 1d : 0d,
            _ => 0d
        };
    }

    public string ToUrqlString(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.String => value.StringValue,
            ValueKind.Number => string.Empty,
            ValueKind.Bool => value.BoolValue ? "1" : "0",
            _ => string.Empty
        };
    }

    public string ToInterpolationString(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.String => value.StringValue,
            ValueKind.Number => value.NumberValue.ToString("G17", CultureInfo.InvariantCulture),
            ValueKind.Bool => value.BoolValue ? "1" : "0",
            _ => string.Empty
        };
    }

    public bool ToBool(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.Number => Math.Abs(value.NumberValue) > 0d,
            ValueKind.String => !string.IsNullOrEmpty(value.StringValue),
            ValueKind.Bool => value.BoolValue,
            _ => false
        };
    }
}
