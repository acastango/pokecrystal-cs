namespace PokeCrystal.Game;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Shared drawing primitives for all scenes.
/// Font and Pixel are set by CrystalGame.LoadContent() before any Draw() runs.
/// Every helper is null-safe — if content fails to load the game still runs,
/// just without visible text or filled rectangles.
/// </summary>
public sealed class GameRenderer
{
    // -------------------------------------------------------------------------
    // Set by CrystalGame.LoadContent
    // -------------------------------------------------------------------------

    public SpriteFont? Font  { get; set; }
    public Texture2D?  Pixel { get; set; }

    // -------------------------------------------------------------------------
    // Palette — Crystal-ish dark-mode colours
    // -------------------------------------------------------------------------

    public static readonly Color BgDark     = new( 16,  16,  24);
    public static readonly Color BgPanel    = new( 32,  32,  40);
    public static readonly Color TextMain   = Color.White;
    public static readonly Color TextDim    = new(176, 176, 192);
    public static readonly Color Cursor     = new(248, 208,  48);
    public static readonly Color HpHigh     = new( 88, 200,  88);
    public static readonly Color HpMid      = new(248, 184,   8);
    public static readonly Color HpLow      = new(248,  56,   8);
    public static readonly Color HpBarBg    = new( 48,  48,  56);
    public static readonly Color GrassTile  = new( 34, 110,  34); // tall grass (bright)
    public static readonly Color FloorTile  = new( 80, 140,  60); // walkable floor (lighter)
    public static readonly Color WallTile   = new( 88,  72,  56); // impassable wall (brown)
    public static readonly Color WaterTile  = new( 32,  80, 160); // water (blue)
    public static readonly Color PlayerTile = new(248, 208,  48);
    public static readonly Color Divider    = new( 64,  64,  72);

    // -------------------------------------------------------------------------
    // Primitive helpers
    // -------------------------------------------------------------------------

    public void FillRect(SpriteBatch sb, int x, int y, int w, int h, Color c)
    {
        if (Pixel is null || w <= 0 || h <= 0) return;
        sb.Draw(Pixel, new Rectangle(x, y, w, h), c);
    }

    public void FillRect(SpriteBatch sb, Rectangle r, Color c)
        => FillRect(sb, r.X, r.Y, r.Width, r.Height, c);

    // -------------------------------------------------------------------------
    // Text helpers
    // -------------------------------------------------------------------------

    public void DrawText(SpriteBatch sb, string text, int x, int y, Color c)
    {
        if (Font is null) return;
        sb.DrawString(Font, text, new Vector2(x, y), c);
    }

    public void DrawText(SpriteBatch sb, string text, Vector2 pos, Color c)
    {
        if (Font is null) return;
        sb.DrawString(Font, text, pos, c);
    }

    /// <summary>Measures text width in pixels. Returns 0 if font not loaded.</summary>
    public int TextWidth(string text) => Font is null ? 0 : (int)Font.MeasureString(text).X;

    /// <summary>Draws text centered horizontally within [x, x+w].</summary>
    public void DrawTextCentered(SpriteBatch sb, string text, int x, int y, int w, Color c)
    {
        if (Font is null) return;
        int tx = x + (w - TextWidth(text)) / 2;
        DrawText(sb, text, tx, y, c);
    }

    // -------------------------------------------------------------------------
    // HP bar
    // -------------------------------------------------------------------------

    /// <summary>
    /// HP bar at (x, y) of size (w × h).
    /// Colour: green > 50%, yellow > 25%, red otherwise.
    /// </summary>
    public void DrawHpBar(SpriteBatch sb, int x, int y, int w, int h, int hp, int maxHp)
    {
        FillRect(sb, x, y, w, h, HpBarBg);
        if (maxHp <= 0) return;
        float ratio = Math.Clamp((float)hp / maxHp, 0f, 1f);
        var fill = ratio > 0.5f ? HpHigh : ratio > 0.25f ? HpMid : HpLow;
        FillRect(sb, x, y, (int)(w * ratio), h, fill);
    }

    // -------------------------------------------------------------------------
    // Full-screen fade overlay
    // -------------------------------------------------------------------------

    public void DrawFade(SpriteBatch sb, float level, int screenW, int screenH)
    {
        if (Pixel is null || level <= 0f) return;
        byte alpha = (byte)Math.Clamp(level * 255f, 0f, 255f);
        FillRect(sb, 0, 0, screenW, screenH, new Color((byte)0, (byte)0, (byte)0, alpha));
    }
}
