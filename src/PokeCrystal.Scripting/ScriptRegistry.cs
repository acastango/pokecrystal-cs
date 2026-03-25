namespace PokeCrystal.Scripting;

/// <summary>
/// Stores named script byte sequences.
/// Scripts are loaded from map data (JSON assets) by the World layer (L4)
/// and registered here before the scripting engine can execute them.
/// </summary>
public sealed class ScriptRegistry
{
    private readonly Dictionary<string, ReadOnlyMemory<byte>> _scripts = new();

    public void Register(string id, ReadOnlyMemory<byte> bytes)
        => _scripts[id] = bytes;

    public void Register(string id, byte[] bytes)
        => _scripts[id] = bytes;

    public ReadOnlyMemory<byte> Get(string id)
    {
        if (!_scripts.TryGetValue(id, out var bytes))
            throw new KeyNotFoundException($"Script '{id}' not registered.");
        return bytes;
    }

    public bool TryGet(string id, out ReadOnlyMemory<byte> bytes)
        => _scripts.TryGetValue(id, out bytes);
}
