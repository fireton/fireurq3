namespace Urql.Core.Runtime;

public sealed class InventoryStore
{
    private readonly Dictionary<string, double> _items = new(StringComparer.OrdinalIgnoreCase);

    public double GetCount(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return 0d;
        }

        return _items.TryGetValue(itemName, out var value) ? value : 0d;
    }

    public void SetCount(string itemName, double count)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return;
        }

        if (count <= 0d)
        {
            _items.Remove(itemName);
            return;
        }

        _items[itemName] = count;
    }

    public void Add(string itemName, double count)
    {
        if (count == 0d)
        {
            return;
        }

        SetCount(itemName, GetCount(itemName) + count);
    }

    public void Remove(string itemName, double count)
    {
        if (count == 0d)
        {
            return;
        }

        SetCount(itemName, GetCount(itemName) - count);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public void Clear(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return;
        }

        _items.Remove(itemName);
    }
}
