using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private readonly Dictionary<Item.Type, int> counts = new Dictionary<Item.Type, int>();

    public event Action<Item.Type, int> OnItemAdded;

    public int CountOf(Item.Type type)
    {
        return counts.TryGetValue(type, out int c) ? c : 0;
    }

    public void Add(Item.Type type)
    {
        counts.TryGetValue(type, out int c);
        c += 1;
        counts[type] = c;
        OnItemAdded?.Invoke(type, c);
    }

    public IEnumerable<KeyValuePair<Item.Type, int>> Counts => counts;

    public void RestoreCounts(IEnumerable<KeyValuePair<Item.Type, int>> source)
    {
        counts.Clear();
        if (source == null) return;
        foreach (var kv in source)
        {
            if (kv.Value <= 0) continue;
            counts[kv.Key] = kv.Value;
            OnItemAdded?.Invoke(kv.Key, kv.Value);
        }
    }
}
