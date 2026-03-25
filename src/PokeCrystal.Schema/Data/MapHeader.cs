namespace PokeCrystal.Schema;

/// <summary>
/// Static map definition header — mirrors maps/*.asm MapHeader macro.
/// Dynamic state (NPC positions, event flags) is separate and mutable.
/// </summary>
public record MapHeader(
    string Id,
    string GroupId,
    int Width,
    int Height,
    string TilesetId,
    string BorderBlockId,
    MapPermission Permission,
    string[] ConnectionIds,  // direction:targetMapId pairs; see PokeCrystal.World.MapConnection
    WarpData[] Warps,
    NpcData[] Npcs,
    TriggerData[] Triggers,
    SignpostData[] Signposts,
    string ScriptRef,
    string EncounterRef
) : IIdentifiable;

public enum MapPermission { Outdoor, Indoor, Cave, Gym }

// MapConnection lives in PokeCrystal.World.MapData — not duplicated here.
public record WarpData(int X, int Y, int WarpId, string DestMapId, int DestWarpId);
public record NpcData(string Id, int X, int Y, string SpriteId, string ScriptRef, string FlagRef, int MovementType);
public record TriggerData(int X, int Y, string ScriptRef, string FlagRef);
public record SignpostData(int X, int Y, SignpostType Type, string ScriptRef);

public enum SignpostType { Script, Item, AutoScript }
