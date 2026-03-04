namespace Urql.Core.Runtime;

public sealed class VariableStore
{
    private readonly Dictionary<string, UrqlValue> _values = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string name, UrqlValue value)
    {
        _values[name] = value;
    }

    public bool TryGet(string name, out UrqlValue value)
    {
        return _values.TryGetValue(name, out value);
    }

    public UrqlValue GetOrDefault(string name)
    {
        return _values.TryGetValue(name, out var value) ? value : UrqlValue.Number(0d);
    }

    public void Clear()
    {
        _values.Clear();
    }
}
