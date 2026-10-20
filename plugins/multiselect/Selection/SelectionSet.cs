using System.Collections.Generic;

namespace TpmMacMultiselect.Selection;

public sealed class SelectionSet
{
    private readonly List<object> _items = new();

    public int Count => _items.Count;

    public bool Add(object item)
    {
        if (item is null || _items.Contains(item)) return false;
        _items.Add(item);
        return true;
    }

    public void Clear() => _items.Clear();

    public IReadOnlyList<object> Snapshot() => _items.ToArray();
}
