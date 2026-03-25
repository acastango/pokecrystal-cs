namespace PokeCrystal.World;

using PokeCrystal.Schema;

/// <summary>
/// Crystal collision constants and permission table.
/// Ported directly from constants/collision_constants.asm and
/// data/collision/collision_permissions.asm.
///
/// Each tile in MapData.Collision[] stores one COLL_* byte.
/// GetPermission() maps that byte to a permission category.
/// </summary>
public static class CollisionConstants
{
    // -----------------------------------------------------------------------
    // COLL_* constants (collision_constants.asm)
    // -----------------------------------------------------------------------

    public const byte Floor          = 0x00;
    public const byte CWall          = 0x07; // generic impassable wall
    public const byte CutTree        = 0x12;
    public const byte LongGrass      = 0x14;
    public const byte HeadbuttTree   = 0x15;
    public const byte TallGrass      = 0x18; // wild-encounter grass
    public const byte Ice            = 0x23;
    public const byte Whirlpool      = 0x24;
    public const byte Water          = 0x29;
    public const byte Waterfall      = 0x33;
    public const byte WarpCarpetDown = 0x70;
    public const byte Door           = 0x71;
    public const byte Ladder         = 0x72;
    public const byte Staircase      = 0x7A;
    public const byte Cave           = 0x7B;
    public const byte WarpPanel      = 0x7C;
    public const byte WarpCarpetRight= 0x7E;
    public const byte Counter        = 0x90;
    public const byte Bookshelf      = 0x91;
    public const byte PC             = 0x93;
    public const byte HopRight       = 0xA0;
    public const byte HopLeft        = 0xA1;
    public const byte HopDown        = 0xA3;
    public const byte HopDownRight   = 0xA4;
    public const byte HopDownLeft    = 0xA5;

    // -----------------------------------------------------------------------
    // Permission flag bits (collision_permissions.asm)
    // -----------------------------------------------------------------------

    public const byte LandTile  = 0x00;
    public const byte WaterTile = 0x01;
    public const byte WallTile  = 0x0F;
    public const byte TalkFlag  = 0x10; // OR'd onto WALL: player can face-interact

    // -----------------------------------------------------------------------
    // Convenience queries
    // -----------------------------------------------------------------------

    /// <summary>True if the player on land can walk onto this tile.</summary>
    public static bool IsLandWalkable(byte coll)
    {
        byte perm = GetPermission(coll);
        return (perm & 0x0F) == LandTile; // land passes; water and wall do not
    }

    /// <summary>True if the tile is tall grass (triggers wild encounters).</summary>
    public static bool IsTallGrass(byte coll) => coll == TallGrass;

    /// <summary>True if the tile triggers a warp when stepped on.</summary>
    public static bool IsWarp(byte coll) => (coll & 0xF0) == 0x70; // HI_NYBBLE_WARPS

    /// <summary>
    /// True if the player can hop this ledge tile while facing <paramref name="dir"/>.
    /// Mirrors Crystal's ledge direction table (ledge_tiles.asm).
    /// </summary>
    public static bool IsLedgeCrossable(byte coll, FacingDirection dir) => coll switch
    {
        HopRight     => dir == FacingDirection.Right,
        HopLeft      => dir == FacingDirection.Left,
        HopDown      => dir == FacingDirection.Down,
        HopDownRight => dir == FacingDirection.Down || dir == FacingDirection.Right,
        HopDownLeft  => dir == FacingDirection.Down || dir == FacingDirection.Left,
        _            => false
    };

    /// <summary>Returns the raw permission byte from the 256-entry table.</summary>
    public static byte GetPermission(byte coll) => s_table[coll];

    // -----------------------------------------------------------------------
    // 256-entry CollisionPermissionTable
    // Direct port of data/collision/collision_permissions.asm
    // -----------------------------------------------------------------------

    private static readonly byte[] s_table = BuildTable();

    private static byte[] BuildTable()
    {
        var t = new byte[256]; // default = LandTile (0x00)

        // 0x07 WALL
        t[0x07] = WallTile;

        // 0x12 CUT_TREE, 0x15 HEADBUTT_TREE: WALL|TALK
        t[0x12] = WallTile | TalkFlag;
        t[0x15] = WallTile | TalkFlag;
        // 0x1A, 0x1D: unused cut/headbutt variants, also WALL|TALK
        t[0x1A] = WallTile | TalkFlag;
        t[0x1D] = WallTile | TalkFlag;

        // 0x20-0x22 water (0x22 has TALK)
        t[0x20] = WaterTile;
        t[0x21] = WaterTile;
        t[0x22] = WaterTile | TalkFlag;
        // 0x23 ICE stays land

        // 0x24 WHIRLPOOL: WATER|TALK
        t[0x24] = WaterTile | TalkFlag;

        // 0x25-0x26 water
        t[0x25] = WaterTile;
        t[0x26] = WaterTile;

        // 0x27 BUOY: WALL
        t[0x27] = WallTile;

        // 0x28-0x29 water
        t[0x28] = WaterTile;
        t[0x29] = WaterTile;

        // 0x2A: WATER|TALK
        t[0x2A] = WaterTile | TalkFlag;

        // 0x2B ICE_2B stays land

        // 0x2C WHIRLPOOL_2C: WATER|TALK
        t[0x2C] = WaterTile | TalkFlag;

        // 0x2D-0x2E water
        t[0x2D] = WaterTile;
        t[0x2E] = WaterTile;

        // 0x2F: WALL
        t[0x2F] = WallTile;

        // 0x30-0x3F: all WATER (waterfall / current)
        for (int i = 0x30; i <= 0x3F; i++) t[i] = WaterTile;

        // 0x40-0x5F: all LAND (brake/walk/grass variants)

        // 0x62: WALL
        t[0x62] = WallTile;

        // 0x6A: WALL
        t[0x6A] = WallTile;

        // 0x70-0x7F: LAND (warp tiles — permission is walkable; WarpSystem handles the transition)

        // 0x80-0x84: WALL
        for (int i = 0x80; i <= 0x84; i++) t[i] = WallTile;

        // 0x88-0x8C: WALL
        for (int i = 0x88; i <= 0x8C; i++) t[i] = WallTile;

        // 0x90-0x9F: WALL (counter, bookshelf, PC, etc.)
        for (int i = 0x90; i <= 0x9F; i++) t[i] = WallTile;

        // 0xA0-0xAF: LAND (ledge hops)
        // 0xB0-0xBF: LAND (directional walls — handled by movement, passable in other directions)

        // 0xC0-0xCF: WATER (buoy directions)
        for (int i = 0xC0; i <= 0xCF; i++) t[i] = WaterTile;

        // 0xD0-0xFE: LAND

        // 0xFF: WALL
        t[0xFF] = WallTile;

        return t;
    }
}
