using PokeCrystal.Schema;

namespace PokeCrystal.Data;

/// <summary>
/// Concrete IDataRegistry backed by per-type Dictionary&lt;string, T&gt; tables.
/// All writes happen during loading; reads after init need no locking.
/// </summary>
public sealed class DataRegistry : IDataRegistry
{
    private readonly Dictionary<Type, object> _tables = new();

    public T Get<T>(string id) where T : IIdentifiable
    {
        var table = GetTable<T>();
        if (!table.TryGetValue(id, out var value))
            throw new KeyNotFoundException($"{typeof(T).Name} '{id}' not found in registry.");
        return value;
    }

    public bool TryGet<T>(string id, out T? value) where T : IIdentifiable
    {
        value = default;
        return GetTable<T>().TryGetValue(id, out value);
    }

    public IReadOnlyList<T> GetAll<T>() where T : IIdentifiable =>
        GetTable<T>().Values.ToList();

    public void Register<T>(T item) where T : IIdentifiable =>
        GetTable<T>()[item.Id] = item;

    private Dictionary<string, T> GetTable<T>() where T : IIdentifiable
    {
        var type = typeof(T);
        if (!_tables.TryGetValue(type, out var table))
        {
            table = new Dictionary<string, T>(StringComparer.Ordinal);
            _tables[type] = table;
        }
        return (Dictionary<string, T>)table;
    }
}
