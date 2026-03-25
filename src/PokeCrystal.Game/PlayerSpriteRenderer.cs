namespace PokeCrystal.Game;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Schema;

/// <summary>
/// Draws the player overworld sprite (Kris) at the correct frame for the current
/// facing direction and walk state, matching Crystal's facings.asm OAM table exactly.
///
/// kris.png layout (16×96 px source, 3× scale → 48×48 screen px per frame):
///   y =  0–15  standing down   FacingStepDown0/2   tiles $00–$03
///   y = 16–31  standing up     FacingStepUp0/2     tiles $04–$07
///   y = 32–47  standing left   FacingStepLeft0/2   tiles $08–$0b
///   y = 48–63  walk down       FacingStepDown1/3   tiles $80–$83
///   y = 64–79  walk up         FacingStepUp1/3     tiles $84–$87
///   y = 80–95  walk left/right FacingStepLeft1/3   tiles $88–$8b
///
/// Flip rules (mirror facings.asm OAM_XFLIP per facing):
///   Standing right  = left tiles + horizontal flip
///   Walking down/up = step1 no flip, step3 flip  (stepParity alternates each step)
///   Walking left    = no flip regardless of parity
///   Walking right   = always flip regardless of parity
/// </summary>
public sealed class PlayerSpriteRenderer
{
    // Native sprite frame size in source pixels
    private const int SrcPx   = 16;
    // 3× scale to match TileSize (48 screen px)
    private const int Scale   = 3;
    private const int ScreenPx = SrcPx * Scale; // 48

    private readonly string _spritesDir;
    private Texture2D? _sheet;
    private Texture2D? _shadow; // 8×8 px source tile, drawn ×2 (normal + x-flip) for 16×8 shadow

    public PlayerSpriteRenderer(string spritesDir)
    {
        _spritesDir = spritesDir;
    }

    /// <summary>Called from CrystalGame.LoadContent once GraphicsDevice is ready.</summary>
    public void Initialize(GraphicsDevice gd)
    {
        _sheet  = LoadColorKeyed(gd, Path.Combine(_spritesDir, "kris.png"));
        _shadow = LoadColorKeyed(gd, Path.Combine(_spritesDir, "shadow.png"));
    }

    private static Texture2D? LoadColorKeyed(GraphicsDevice gd, string path)
    {
        if (!File.Exists(path)) return null;
        Texture2D tex;
        using (var stream = File.OpenRead(path))
            tex = Texture2D.FromStream(gd, stream);
        // GB color 0 (white) is the transparent color — key it out manually (no alpha channel).
        var pixels = new Color[tex.Width * tex.Height];
        tex.GetData(pixels);
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].R > 240 && pixels[i].G > 240 && pixels[i].B > 240)
                pixels[i] = Color.Transparent;
        tex.SetData(pixels);
        return tex;
    }

    /// <summary>
    /// Draw the hop shadow at ground level (no arc offset).
    /// Matches Crystal's FacingShadow: tile $fc normal + tile $fc x-flipped, side by side.
    /// Source is 8×8 px; rendered as two 24×24 tiles → 48×24 screen px total.
    /// </summary>
    /// <summary>
    /// Draws the hop shadow at ground level (no arc offset).
    /// Y offset mirrors Crystal's MovementFunction_Shadow: 14 GB px (DOWN/UP) or 12 GB px (LEFT/RIGHT),
    /// scaled ×3 = 42 or 36 screen px below the sprite origin.
    /// </summary>
    public void DrawShadow(SpriteBatch sb, int screenX, int screenY, FacingDirection facing)
    {
        if (_shadow is null) return;

        // Crystal MovementFunction_Shadow: OBJECT_SPRITE_Y_OFFSET = 1*TILE_WIDTH+6 (DOWN/UP) or 1*TILE_WIDTH+4 (LEFT/RIGHT)
        // GB pixels × 3 scale = screen pixels
        int yOfs = (facing == FacingDirection.Down || facing == FacingDirection.Up) ? 42 : 36;

        var src  = new Rectangle(0, 0, _shadow.Width, _shadow.Height); // full 8×8 tile
        int tw   = SrcPx * Scale / 2; // 24 px — shadow tile is half the sprite width
        int th   = tw;                // square tile

        // Left half: normal; Right half: x-flipped
        var dstL = new Rectangle(screenX,      screenY + yOfs, tw, th);
        var dstR = new Rectangle(screenX + tw, screenY + yOfs, tw, th);

        sb.Draw(_shadow, dstL, src, Color.White, 0f, Vector2.Zero, SpriteEffects.None,             0f);
        sb.Draw(_shadow, dstR, src, Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
    }

    /// <summary>
    /// Draw the player sprite at screen position (screenX, screenY).
    /// <paramref name="isStepping"/>: true while a walk step is in progress.
    /// <paramref name="stepParity"/>: alternates each step; drives foot alternation for Down/Up.
    /// </summary>
    public void Draw(SpriteBatch sb, FacingDirection facing,
                     bool isStepping, bool stepParity,
                     int screenX, int screenY)
    {
        if (_sheet is null) return;

        int srcY;
        bool flip;

        if (isStepping)
        {
            switch (facing)
            {
                case FacingDirection.Down:
                    // FacingStepDown1 (no flip) ↔ FacingStepDown3 (flip) — foot alternation
                    srcY = 48;
                    flip = stepParity;
                    break;
                case FacingDirection.Up:
                    // FacingStepUp1 (no flip) ↔ FacingStepUp3 (flip)
                    srcY = 64;
                    flip = stepParity;
                    break;
                case FacingDirection.Left:
                    // FacingStepLeft1 = FacingStepLeft3 (identical tiles, no flip).
                    // Alternate walk frame ↔ standing frame each step so the cycle
                    // is visually balanced (matches Crystal's stand→walk→stand→walk).
                    srcY = stepParity ? 80 : 32;
                    flip = false;
                    break;
                default: // Right — mirror of Left with OAM_XFLIP throughout
                    srcY = stepParity ? 80 : 32;
                    flip = true;
                    break;
            }
        }
        else
        {
            // Standing rows
            srcY = facing switch
            {
                FacingDirection.Up    => 16,
                FacingDirection.Left  => 32,
                FacingDirection.Right => 32,
                _                     =>  0,  // Down
            };
            flip = facing == FacingDirection.Right;
        }

        var src = new Rectangle(0, srcY, SrcPx, SrcPx);
        var dst = new Rectangle(screenX, screenY, ScreenPx, ScreenPx);
        var fx  = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        sb.Draw(_sheet, dst, src, Color.White, 0f, Vector2.Zero, fx, 0f);
    }

    public void Dispose() { _sheet?.Dispose(); _shadow?.Dispose(); }
}
