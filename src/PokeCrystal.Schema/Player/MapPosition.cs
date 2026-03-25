namespace PokeCrystal.Schema;

/// <summary>
/// Player's current map location — mirrors wCurMapData WRAM block.
/// MapId is a string key resolved from (MapGroup, MapNumber) at load time.
/// </summary>
public record MapPosition(string MapId, int X, int Y, FacingDirection Facing);

public enum FacingDirection { Down, Up, Left, Right }
