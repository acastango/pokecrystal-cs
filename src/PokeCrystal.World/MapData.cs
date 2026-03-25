namespace PokeCrystal.World;

using PokeCrystal.Schema;

/// <summary>
/// Full runtime map definition loaded from data/base/maps/{MAP_ID}.json.
/// Extends the Schema MapHeader with wild encounter tables, NPCs, warps, and events.
/// </summary>
public sealed class MapData
{
    public required string Id { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public byte BorderBlock { get; init; }
    public MapConnection[] Connections { get; init; } = [];
    public NpcData[] Npcs { get; init; } = [];
    public WarpData[] Warps { get; init; } = [];
    public CoordEvent[] CoordEvents { get; init; } = [];
    public BgEvent[] BgEvents { get; init; } = [];
    public WildGrassTable? WildGrass { get; init; }
    public WildWaterTable? WildWater { get; init; }
    public string? MapScriptId { get; init; }   // entry script (null = no script)
    public string MusicId { get; init; } = string.Empty;
    public string Environment { get; init; } = "INDOOR";

    /// <summary>
    /// Per-tile collision bytes, row-major (index = y * Width + x).
    /// Values are COLL_* constants from CollisionConstants.
    /// Empty = no collision data (all tiles treated as Floor).
    /// </summary>
    public byte[] Collision { get; init; } = [];

    /// <summary>Returns the COLL_* byte at (x, y), or Floor if out of bounds / no data.</summary>
    public byte GetCollision(int x, int y)
    {
        if (Collision.Length == 0) return CollisionConstants.Floor;
        int idx = y * Width + x;
        return (uint)idx < (uint)Collision.Length ? Collision[idx] : CollisionConstants.CWall;
    }

    /// <summary>
    /// Tileset name (e.g. "johto") used to look up metatile graphics.
    /// </summary>
    public string TilesetId { get; init; } = string.Empty;

    /// <summary>Width in metatile blocks (Width = BlkWidth * 2).</summary>
    public int BlkWidth  { get; init; }

    /// <summary>Height in metatile blocks (Height = BlkHeight * 2).</summary>
    public int BlkHeight { get; init; }

    /// <summary>
    /// Raw metatile block indices from the .blk file, row-major (index = by * BlkWidth + bx).
    /// Each value is an index into the tileset's metatile table (0–127).
    /// </summary>
    public byte[] Blocks { get; init; } = [];

    /// <summary>Returns the metatile block index at block-coord (bx, by), or 0 if out of bounds.</summary>
    public byte GetBlock(int bx, int by)
    {
        if (Blocks.Length == 0 || BlkWidth == 0) return 0;
        int idx = by * BlkWidth + bx;
        return (uint)idx < (uint)Blocks.Length ? Blocks[idx] : (byte)0;
    }
}

/// <summary>
/// Edge connection to an adjacent map.
/// Direction = "north"/"south"/"west"/"east".
/// Offset adjusts the perpendicular coordinate when crossing:
///   N/S crossing: newX = currentX + Offset
///   E/W crossing: newY = currentY + Offset
/// Mirrors Crystal's connection macro \4 parameter (tile units).
/// </summary>
public record MapConnection(string Direction, string TargetMapId, int Offset);

public record NpcData(int Id, string SpriteId, int X, int Y,
    string MovementType, string ScriptId, bool Hidden = false);

public record WarpData(int X, int Y, string TargetMapId, int TargetWarpId);

public record CoordEvent(int X, int Y, string ScriptId);

public record BgEvent(int X, int Y, string TextId);

public record WildSlot(int Level, string SpeciesId);

public record WildGrassTable(
    int MornRate, int DayRate, int NiteRate,
    WildSlot[] Morn, WildSlot[] Day, WildSlot[] Nite);

public record WildWaterTable(int Rate, WildSlot[] Slots);
