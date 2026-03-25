namespace PokeCrystal.World;

using System.Text.Json;
using System.Text.Json.Serialization;
using PokeCrystal.Scripting;

/// <summary>
/// Loads MapData from JSON files in data/base/maps/ and registers them.
/// Also registers any map entry scripts into the ScriptRegistry.
/// </summary>
public sealed class MapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MapRegistry _mapRegistry;
    private readonly ScriptRegistry _scriptRegistry;

    public MapLoader(MapRegistry mapRegistry, ScriptRegistry scriptRegistry)
    {
        _mapRegistry = mapRegistry;
        _scriptRegistry = scriptRegistry;
    }

    public void LoadAll(string mapsDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(mapsDirectory, "*.json"))
            LoadFile(file);
    }

    public MapData LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<MapDataDto>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to parse map file: {path}");

        var map = dto.ToMapData();
        _mapRegistry.Register(map);
        return map;
    }

    // DTO for deserialization — mirrors the JSON schema
    private sealed class MapDataDto
    {
        public string Id { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public byte BorderBlock { get; set; }
        public MapConnectionDto[]? Connections { get; set; }
        public NpcDataDto[]? Npcs { get; set; }
        public WarpDataDto[]? Warps { get; set; }
        public CoordEventDto[]? CoordEvents { get; set; }
        public BgEventDto[]? BgEvents { get; set; }
        public WildGrassTableDto? WildGrass { get; set; }
        public WildWaterTableDto? WildWater { get; set; }
        public string? MapScriptId { get; set; }
        public string MusicId { get; set; } = string.Empty;
        public string Environment { get; set; } = "INDOOR";
        // JSON number arrays deserialise cleanly as int[]; byte[] would expect Base64.
        public int[]? Collision { get; set; }
        public string? TilesetId { get; set; }
        public int BlkWidth  { get; set; }
        public int BlkHeight { get; set; }
        public int[]? Blocks { get; set; }

        public MapData ToMapData() => new()
        {
            Id           = Id,
            Width        = Width,
            Height       = Height,
            BorderBlock  = BorderBlock,
            Connections  = Connections?.Select(c => c.ToRecord()).ToArray() ?? [],
            Npcs         = Npcs?.Select(n => n.ToRecord()).ToArray() ?? [],
            Warps        = Warps?.Select(w => w.ToRecord()).ToArray() ?? [],
            CoordEvents  = CoordEvents?.Select(e => e.ToRecord()).ToArray() ?? [],
            BgEvents     = BgEvents?.Select(e => e.ToRecord()).ToArray() ?? [],
            WildGrass    = WildGrass?.ToRecord(),
            WildWater    = WildWater?.ToRecord(),
            MapScriptId  = MapScriptId,
            MusicId      = MusicId,
            Environment  = Environment,
            Collision    = Collision is null ? [] : Array.ConvertAll(Collision, v => (byte)v),
            TilesetId    = TilesetId ?? string.Empty,
            BlkWidth     = BlkWidth,
            BlkHeight    = BlkHeight,
            Blocks       = Blocks is null ? [] : Array.ConvertAll(Blocks, v => (byte)v),
        };
    }

    private sealed class MapConnectionDto
    {
        public string Direction { get; set; } = string.Empty;
        public string TargetMapId { get; set; } = string.Empty;
        public int Offset { get; set; }
        public MapConnection ToRecord() => new(Direction, TargetMapId, Offset);
    }

    private sealed class NpcDataDto
    {
        public int Id { get; set; }
        public string SpriteId { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public string MovementType { get; set; } = "Stationary";
        public string ScriptId { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public NpcData ToRecord() => new(Id, SpriteId, X, Y, MovementType, ScriptId, Hidden);
    }

    private sealed class WarpDataDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string TargetMapId { get; set; } = string.Empty;
        public int TargetWarpId { get; set; }
        public WarpData ToRecord() => new(X, Y, TargetMapId, TargetWarpId);
    }

    private sealed class CoordEventDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string ScriptId { get; set; } = string.Empty;
        public CoordEvent ToRecord() => new(X, Y, ScriptId);
    }

    private sealed class BgEventDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string TextId { get; set; } = string.Empty;
        public BgEvent ToRecord() => new(X, Y, TextId);
    }

    private sealed class WildSlotDto
    {
        public int Level { get; set; }
        public string SpeciesId { get; set; } = string.Empty;
        public WildSlot ToRecord() => new(Level, SpeciesId);
    }

    private sealed class WildGrassTableDto
    {
        public int MornRate { get; set; }
        public int DayRate { get; set; }
        public int NiteRate { get; set; }
        public WildSlotDto[]? Morn { get; set; }
        public WildSlotDto[]? Day { get; set; }
        public WildSlotDto[]? Nite { get; set; }
        public WildGrassTable ToRecord() => new(
            MornRate, DayRate, NiteRate,
            Morn?.Select(s => s.ToRecord()).ToArray() ?? [],
            Day?.Select(s => s.ToRecord()).ToArray() ?? [],
            Nite?.Select(s => s.ToRecord()).ToArray() ?? []);
    }

    private sealed class WildWaterTableDto
    {
        public int Rate { get; set; }
        public WildSlotDto[]? Slots { get; set; }
        public WildWaterTable ToRecord() => new(Rate,
            Slots?.Select(s => s.ToRecord()).ToArray() ?? []);
    }
}
