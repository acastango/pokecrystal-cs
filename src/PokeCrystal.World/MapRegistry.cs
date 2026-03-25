namespace PokeCrystal.World;

/// <summary>
/// Stores all loaded MapData instances by ID.
/// Populated by MapLoader at startup.
/// </summary>
public sealed class MapRegistry
{
    private readonly Dictionary<string, MapData> _maps = new();

    public void Register(MapData map) => _maps[map.Id] = map;

    public MapData Get(string mapId)
    {
        if (!_maps.TryGetValue(mapId, out var map))
            throw new KeyNotFoundException($"Map '{mapId}' not registered.");
        return map;
    }

    public bool TryGet(string mapId, out MapData? map)
        => _maps.TryGetValue(mapId, out map);

    public IReadOnlyCollection<MapData> All => _maps.Values;
}
