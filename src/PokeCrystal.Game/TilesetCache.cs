namespace PokeCrystal.Game;

using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Loads Crystal tileset data (metatile .bin + PNG tile sheet) and draws tiles.
///
/// Crystal tileset structure:
///   - 1 tile   = 8×8 Game Boy pixels (2bpp on hardware; stored as 2-bit grayscale PNG)
///   - 1 block  = 4×4 tiles = 32×32 GB pixels (one entry in the .blk file)
///   - metatile .bin = 128 blocks × 16 bytes (one byte = tile index, row-major 4×4)
///
/// Our C# coordinate system uses "half-block" units (2×2 tiles = 16×16 GB px)
/// matching the collision table resolution.  One screen cell = TileSize = 48px
/// at 3× scale, drawn from a 2×2 sub-grid of the 4×4 tile block.
///
/// Palette:
///   MonoGame loads the 2-bit PNG and expands to R=0/85/170/255 (dark→light).
///   PNG value 3 (R=255, white) = GBC palette color 0 (lightest / background).
///   PNG value 0 (R=0,   black) = GBC palette color 3 (darkest  / outlines).
///   At load time we recolor each tile in-place using the GBC BG palette data
///   from {tilesetId}_tile_palettes.bin and bg_palettes.json.
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
    private const int TilesPerSheet = 16; // tileset PNGs are 16 tiles wide

    // Palette name order — must match gen_tilesets.js nameOrder
    private static readonly string[] PaletteNames =
        ["gray", "red", "green", "water", "yellow", "brown", "roof", "text"];

    private record TilesetEntry(Texture2D Sheet, byte[] Metatiles);

    private readonly Dictionary<string, TilesetEntry> _cache = new();
    private readonly string _tilesetsDir;
    private GraphicsDevice _gd = null!;

    // Shared palette colors loaded once from bg_palettes.json; null until first use
    private Color[][]? _palettes;

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

        var pngPath    = Path.Combine(_tilesetsDir, $"{tilesetId}.png");
        var binPath    = Path.Combine(_tilesetsDir, $"{tilesetId}_metatiles.bin");
        var palMapPath = Path.Combine(_tilesetsDir, $"{tilesetId}_tile_palettes.bin");
        var palJsonPath= Path.Combine(_tilesetsDir, "bg_palettes.json");

        Texture2D sheet;
        using (var stream = File.OpenRead(pngPath))
            sheet = Texture2D.FromStream(_gd, stream);

        var metatiles = File.ReadAllBytes(binPath);

        // Apply GBC palette coloring if data files are present
        if (File.Exists(palMapPath) && File.Exists(palJsonPath))
        {
            _palettes ??= LoadPaletteColors(palJsonPath);
            var tilePaletteMap = File.ReadAllBytes(palMapPath);
            ApplyPalettes(sheet, tilePaletteMap, _palettes);
        }

        var entry = new TilesetEntry(sheet, metatiles);
        _cache[tilesetId] = entry;
        return entry;
    }

    // ── Palette loading ────────────────────────────────────────────────────────

    private static Color[][] LoadPaletteColors(string jsonPath)
    {
        // Fallback grayscale — used if JSON can't be parsed
        var fallback = new Color[][]
        {
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // gray
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // red
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // green
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // water
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // yellow
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // brown
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // roof
            [Color.White, new Color(170,170,170), new Color(85,85,85), Color.Black], // text
        };

        try
        {
            using var doc  = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root       = doc.RootElement;
            var result     = new Color[PaletteNames.Length][];

            for (int i = 0; i < PaletteNames.Length; i++)
            {
                if (!root.TryGetProperty(PaletteNames[i], out var palEl))
                { result[i] = fallback[i]; continue; }

                var colors = new Color[4];
                int j = 0;
                foreach (var c in palEl.EnumerateArray())
                {
                    if (j >= 4) break;
                    colors[j++] = new Color(
                        c.GetProperty("r").GetInt32(),
                        c.GetProperty("g").GetInt32(),
                        c.GetProperty("b").GetInt32(),
                        255);
                }
                result[i] = colors;
            }
            return result;
        }
        catch { return fallback; }
    }

    // ── Palette application ────────────────────────────────────────────────────

    private static void ApplyPalettes(Texture2D sheet, byte[] tilePaletteMap, Color[][] palettes)
    {
        var pixels = new Color[sheet.Width * sheet.Height];
        sheet.GetData(pixels);

        int tilesWide = sheet.Width / CrystalTilePx;
        int tilesHigh = sheet.Height / CrystalTilePx;
        int totalTiles = tilesWide * tilesHigh;

        for (int tileIdx = 0; tileIdx < totalTiles; tileIdx++)
        {
            int palIdx = tileIdx < tilePaletteMap.Length ? tilePaletteMap[tileIdx] : 0;
            if (palIdx >= palettes.Length) palIdx = 0;
            var pal = palettes[palIdx];

            int tileOriginX = (tileIdx % tilesWide) * CrystalTilePx;
            int tileOriginY = (tileIdx / tilesWide) * CrystalTilePx;

            for (int py = 0; py < CrystalTilePx; py++)
            {
                for (int px = 0; px < CrystalTilePx; px++)
                {
                    int idx = (tileOriginY + py) * sheet.Width + (tileOriginX + px);
                    if (idx >= pixels.Length) continue;

                    // 2-bit PNG: R=255→GBC0 (lightest), R=0→GBC3 (darkest)
                    // Map R → GBC color index: 0=(255), 1=(170), 2=(85), 3=(0)
                    int gbc = (255 - pixels[idx].R + 42) / 85;
                    pixels[idx] = pal[Math.Clamp(gbc, 0, 3)];
                }
            }
        }

        sheet.SetData(pixels);
    }

    // ── Drawing ────────────────────────────────────────────────────────────────

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
