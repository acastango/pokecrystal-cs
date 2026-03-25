namespace PokeCrystal.Game;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Loads Crystal tileset data (metatile .bin + PNG tile sheet) and draws tiles.
///
/// Crystal tileset structure:
///   - 1 tile   = 8×8 Game Boy pixels (2bpp on hardware; stored as PNG)
///   - 1 block  = 4×4 tiles = 32×32 GB pixels (one entry in the .blk file)
///   - metatile .bin = 128 blocks × 16 bytes (one byte = tile index, row-major 4×4)
///
/// Our C# coordinate system uses "half-block" units (2×2 tiles = 16×16 GB px)
/// matching the collision table resolution.  One screen cell = TileSize = 48px
/// at 3× scale, drawn from a 2×2 sub-grid of the 4×4 tile block.
/// </summary>
public sealed class TilesetCache
{
    // Scale factor: 1 GB pixel → 3 screen pixels
    private const int Scale = 3;
    // Crystal tile size in Game Boy pixels
    private const int CrystalTilePx = 8;
    // Each tile drawn at scale: 8 × 3 = 24 screen pixels
    private const int ScreenTilePx = CrystalTilePx * Scale;
    // Crystal metatile is 4×4 tiles, 16 bytes per entry
    private const int TilesPerBlock = 4;
    private const int BytesPerBlock = TilesPerBlock * TilesPerBlock;
    private const int TilesPerSheet = 16; // johto.png is 16 tiles wide

    // tile-sheet rows held per tileset PNG
    private record TilesetEntry(Texture2D Sheet, byte[] Metatiles);

    private readonly Dictionary<string, TilesetEntry> _cache = new();
    private readonly string _tilesetsDir;
    private GraphicsDevice _gd = null!;

    public TilesetCache(string tilesetsDir)
    {
        _tilesetsDir = tilesetsDir;
    }

    /// <summary>Called from LoadContent once GraphicsDevice is ready.</summary>
    public void Initialize(GraphicsDevice gd)
    {
        _gd = gd;
    }

    private TilesetEntry Load(string tilesetId)
    {
        if (_cache.TryGetValue(tilesetId, out var cached)) return cached;

        var pngPath = Path.Combine(_tilesetsDir, $"{tilesetId}.png");
        var binPath = Path.Combine(_tilesetsDir, $"{tilesetId}_metatiles.bin");

        Texture2D sheet;
        using (var stream = File.OpenRead(pngPath))
            sheet = Texture2D.FromStream(_gd, stream);

        var metatiles = File.ReadAllBytes(binPath);

        var entry = new TilesetEntry(sheet, metatiles);
        _cache[tilesetId] = entry;
        return entry;
    }

    /// <summary>
    /// Draw a 2×2-tile quadrant of a Crystal metatile block at screen position (sx, sy).
    /// The rendered size is 48×48 px (2 tiles × 8 px × 3 scale).
    ///
    /// <paramref name="blockIdx"/>: 0-127 metatile index from .blk data.
    /// <paramref name="qx"/>: 0=left half, 1=right half.
    /// <paramref name="qy"/>: 0=top half,  1=bottom half.
    /// </summary>
    public void DrawQuadrant(SpriteBatch sb, string tilesetId,
        int blockIdx, int qx, int qy, int sx, int sy)
    {
        if (_gd is null) return;

        TilesetEntry ts;
        try { ts = Load(tilesetId); }
        catch { return; }  // tileset not found — silently skip

        var metatiles = ts.Metatiles;
        int baseOffset = blockIdx * BytesPerBlock;
        if (baseOffset + BytesPerBlock > metatiles.Length) return;

        // Quadrant (qx, qy) covers tile sub-positions (qx*2, qy*2)–(qx*2+1, qy*2+1)
        // within the 4×4 tile grid of this block.
        for (int subY = 0; subY < 2; subY++)
        {
            for (int subX = 0; subX < 2; subX++)
            {
                int tileCol = qx * 2 + subX;  // 0–3 within the 4-wide metatile
                int tileRow = qy * 2 + subY;  // 0–3 within the 4-tall metatile
                int tileIdx = metatiles[baseOffset + tileRow * TilesPerBlock + tileCol];

                // Source region in the tile sheet (16 tiles wide, 8px each)
                int srcX = (tileIdx % TilesPerSheet) * CrystalTilePx;
                int srcY = (tileIdx / TilesPerSheet) * CrystalTilePx;
                var src = new Rectangle(srcX, srcY, CrystalTilePx, CrystalTilePx);

                // Destination on screen
                int dstX = sx + subX * ScreenTilePx;
                int dstY = sy + subY * ScreenTilePx;
                var dst = new Rectangle(dstX, dstY, ScreenTilePx, ScreenTilePx);

                sb.Draw(ts.Sheet, dst, src, Color.White);
            }
        }
    }

    public void Dispose()
    {
        foreach (var e in _cache.Values)
            e.Sheet.Dispose();
        _cache.Clear();
    }
}
