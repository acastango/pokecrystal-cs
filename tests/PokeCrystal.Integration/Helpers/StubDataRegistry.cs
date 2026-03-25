namespace PokeCrystal.Integration.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using PokeCrystal.Data;
using PokeCrystal.Schema;

/// <summary>
/// Minimal IDataRegistry backed by an in-memory dictionary.
/// Used by BattleEngineTests to register only the moves/species needed per test
/// without loading data/base/ from disk.
/// </summary>
public sealed class StubDataRegistry : IDataRegistry
{
    // Key: "TypeName\0Id"
    private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

    public void Register<T>(T item) where T : IIdentifiable
        => _items[$"{typeof(T).Name}\0{item.Id}"] = item;

    public T Get<T>(string id) where T : IIdentifiable
    {
        if (TryGet<T>(id, out var value)) return value!;
        throw new KeyNotFoundException($"{typeof(T).Name} '{id}' not registered in StubDataRegistry.");
    }

    public bool TryGet<T>(string id, out T? value) where T : IIdentifiable
    {
        if (_items.TryGetValue($"{typeof(T).Name}\0{id}", out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public IReadOnlyList<T> GetAll<T>() where T : IIdentifiable
        => _items.Values.OfType<T>().ToList();
}
