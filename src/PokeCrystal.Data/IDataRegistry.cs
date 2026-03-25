using PokeCrystal.Schema;

namespace PokeCrystal.Data;

/// <summary>
/// Typed lookup for all L0 schema data, keyed by string ID.
/// Loaded eagerly at startup; thread-safe reads after initialization.
/// Mods extend it via Register&lt;T&gt; before game start.
/// </summary>
public interface IDataRegistry
{
    /// <summary>Returns the item with the given ID. Throws KeyNotFoundException if missing.</summary>
    T Get<T>(string id) where T : IIdentifiable;

    /// <summary>Returns false (and default) if not found.</summary>
    bool TryGet<T>(string id, out T? value) where T : IIdentifiable;

    /// <summary>Returns all registered items of type T.</summary>
    IReadOnlyList<T> GetAll<T>() where T : IIdentifiable;

    /// <summary>
    /// Register or replace an item. Used by mods and the loader itself.
    /// Replaces any existing entry with the same ID.
    /// </summary>
    void Register<T>(T item) where T : IIdentifiable;
}
