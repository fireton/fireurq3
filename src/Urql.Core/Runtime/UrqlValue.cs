namespace Urql.Core.Runtime;

public readonly record struct UrqlValue
{
    public ValueKind Kind { get; }
    public double NumberValue { get; }
    public string StringValue { get; }
    public bool BoolValue { get; }

    private UrqlValue(ValueKind kind, double numberValue, string stringValue, bool boolValue)
    {
        Kind = kind;
        NumberValue = numberValue;
        StringValue = stringValue;
        BoolValue = boolValue;
    }

    public static UrqlValue Number(double value) => new(ValueKind.Number, value, string.Empty, false);
    public static UrqlValue String(string value) => new(ValueKind.String, 0d, value ?? string.Empty, false);
    public static UrqlValue Bool(bool value) => new(ValueKind.Bool, 0d, string.Empty, value);
}
