namespace PokeCrystal.Game.Scenes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Schema;
using XnaGameTime = Microsoft.Xna.Framework.GameTime;
using PokeCrystal.Rendering;
using PokeCrystal.World;
using PokeCrystal.World.Systems;

/// <summary>
/// Overworld scene. Bridges L4 (WorldContext/OverworldEngine) with the
/// input system (L6) and audio/rendering (L5).
///
/// Each frame:
///   1. Read input → set PlayerController.PendingDirection
///   2. OverworldEngine.Tick(ctx) — runs the world
///   3. Drain pending* fields from ctx (warp, battle, music, sound)
/// </summary>
public sealed class OverworldScene : IScene
{
    // Tile size in screen pixels (160×144 GB × 3× scale = 480×432)
    private const int TileSize   = 48;
    private const int ViewCols   = 10;
    private const int ViewRows   = 9;
    private const int ScreenW    = 480;
    private const int ScreenH    = 432;
    private const int StatusBarH = 28;

    private readonly OverworldEngine      _engine;
    private readonly WorldContext         _ctx;
    private readonly PlayerController     _playerController;
    private readonly MapObjectSystem      _mapObjects;
    private readonly IAudioRenderer       _audio;
    private readonly IInputProvider       _input;
    private readonly SceneManager         _scenes;
    private readonly BattleScene          _battleScene;
    private readonly StartMenuScene       _startMenu;
    private readonly GameRenderer         _renderer;
    private readonly TilesetCache         _tilesets;
    private readonly PlayerSpriteRenderer _playerSprite;
    private readonly DebugConsole         _debug;

    public OverworldScene(
        OverworldEngine      engine,
        WorldContext         ctx,
        PlayerController     playerController,
        MapObjectSystem      mapObjects,
        IAudioRenderer       audio,
        IInputProvider       input,
        SceneManager         scenes,
        BattleScene          battleScene,
        StartMenuScene       startMenu,
        GameRenderer         renderer,
        TilesetCache         tilesets,
        PlayerSpriteRenderer playerSprite,
        DebugConsole         debug)
    {
        _engine           = engine;
        _ctx              = ctx;
        _playerController = playerController;
        _playerController.Logger = msg =>
        {
            if (!GameLog.EnableMovement) return;
            GameLog.Write(msg);
            // Draw ASCII map snapshot after terminal events (not the noisy per-frame TRY lines)
            if (msg.StartsWith("[STEP]") || msg.StartsWith("[HOP!]") || msg.StartsWith("[BLOCKED]"))
                foreach (var line in BuildAsciiMap())
                    GameLog.Write(line);
        };
        _mapObjects       = mapObjects;
        _audio            = audio;
        _input            = input;
        _scenes           = scenes;
        _battleScene      = battleScene;
        _startMenu        = startMenu;
        _renderer         = renderer;
        _tilesets         = tilesets;
        _playerSprite     = playerSprite;
        _debug            = debug;
    }

    public void OnEnter()
    {
        _ctx.MapStatus             = MapStatus.Start;
        _ctx.EventsEnabled         = false;
        _ctx.WildEncounterCooldown = 90; // prevent instant re-encounter after battle
    }

    public void OnExit() { }

    public void Update(XnaGameTime gameTime)
    {
        // ~ toggles debug console; when open, swallow all gameplay input
        if (_input.IsPressed(GameAction.DebugConsole))
            _debug.Toggle();

        if (_debug.IsOpen)
        {
            _engine.Tick(_ctx);
            return;
        }

        // --- Input → PlayerController ---
        if (_input.IsHeld(GameAction.MoveUp))         _playerController.PendingDirection = FacingDirection.Up;
        else if (_input.IsHeld(GameAction.MoveDown))  _playerController.PendingDirection = FacingDirection.Down;
        else if (_input.IsHeld(GameAction.MoveLeft))  _playerController.PendingDirection = FacingDirection.Left;
        else if (_input.IsHeld(GameAction.MoveRight)) _playerController.PendingDirection = FacingDirection.Right;
        else _playerController.PendingDirection = null; // clear stale direction when no key held

        // A-press → try interact with facing NPC/sign
        if (_input.IsPressed(GameAction.Confirm))
            _mapObjects.RequestInteract();

        // B/Start → open start menu
        if (_input.IsPressed(GameAction.Menu))
            _scenes.Transition(_startMenu);

        // --- World tick ---
        _engine.Tick(_ctx);

        // --- Drain pending fields ---

        if (_ctx.PendingMusic is { } music)
        {
            _audio.PlayMusic(music);
            _ctx.PendingMusic = null;
        }

        if (_ctx.PendingSound is { } sound)
        {
            _audio.PlaySfx(sound);
            _ctx.PendingSound = null;
        }

        if (_ctx.PendingBattle is { } battle)
        {
            _ctx.PendingBattle = null;
            _battleScene.Setup = battle;
            _scenes.Transition(_battleScene);
        }
        else if (_ctx.PendingWarpMapId is { } warpMapId)
        {
            _ctx.PendingWarpMapId = null;
            ExecuteWarp(warpMapId, _ctx.PendingWarpId);
        }
    }

    public void Draw(SpriteBatch sb)
    {
        // Background
        _renderer.FillRect(sb, 0, 0, ScreenW, ScreenH, GameRenderer.BgDark);

        // Resolve map (may be null if not yet loaded)
        MapData? map = null;
        bool hasMap = _ctx.Maps is not null &&
                      _ctx.Maps.TryGet(_ctx.CurrentMapId, out map) &&
                      map is not null;

        int mapW = hasMap && map is not null ? map.Width  : 20;
        int mapH = hasMap && map is not null ? map.Height : 9;

        // Visual (interpolated) player position in tile units.
        // SubTileOffset is in [-1,+1]: 0 = on tile, ±1 = one tile away from destination.
        float visX = _ctx.PlayerX + _playerController.SubTileOffsetX;
        float visY = _ctx.PlayerY + _playerController.SubTileOffsetY;

        // Float camera centred on the visual position, clamped to map bounds.
        // Using the visual position means camXf changes smoothly — no per-frame jump.
        float camXf = Math.Clamp(visX - ViewCols / 2f, 0f, Math.Max(0f, mapW - ViewCols));
        float camYf = Math.Clamp(visY - ViewRows / 2f, 0f, Math.Max(0f, mapH - ViewRows));

        // Split into integer tile index + sub-tile pixel remainder.
        int   camX    = (int)Math.Floor(camXf);
        int   camY    = (int)Math.Floor(camYf);
        float pixOfsX = -(camXf - camX) * TileSize;   // fractional pixel nudge
        float pixOfsY = -(camYf - camY) * TileSize;

        // Draw tiles — loop ±1 beyond viewport to fill the entering edge during scroll
        for (int ty = -1; ty <= ViewRows; ty++)
        {
            for (int tx = -1; tx <= ViewCols; tx++)
            {
                int sx = (int)(tx * TileSize + pixOfsX);
                int sy = (int)(ty * TileSize + pixOfsY);

                // Cull tiles entirely outside the game area
                if (sx + TileSize <= 0 || sx >= ScreenW ||
                    sy + TileSize <= 0 || sy >= ScreenH - StatusBarH)
                    continue;

                int mx = camX + tx;
                int my = camY + ty;
                bool inBounds = mx >= 0 && my >= 0 && mx < mapW && my < mapH;

                if (!inBounds || !hasMap || map is null || string.IsNullOrEmpty(map.TilesetId))
                {
                    _renderer.FillRect(sb, sx, sy, TileSize, TileSize, GameRenderer.BgDark);
                    continue;
                }

                // Convert 2×2-tile (half-block) coord → block + quadrant in the 4×4 metatile
                int bx = mx / 2;
                int by = my / 2;
                int qx = mx % 2;
                int qy = my % 2;
                int blockIdx = map.GetBlock(bx, by);

                _tilesets.DrawQuadrant(sb, map.TilesetId, blockIdx, qx, qy, sx, sy);
            }
        }

        // Player is always at the visual centre: (visX - camXf) == ViewCols/2 when unclamped.
        int playerSX = (int)((visX - camXf) * TileSize);
        int playerSY = (int)((visY - camYf) * TileSize);

        // During a hop: shadow drawn at scroll position (follows player),
        // offset down to foot level (bottom quarter of the 48px tile).
        if (_playerController.IsHopping)
            _playerSprite.DrawShadow(sb, playerSX, playerSY, _ctx.Facing);

        _playerSprite.Draw(sb, _ctx.Facing,
            _playerController.IsStepping, _playerController.StepParity,
            playerSX, playerSY + _playerController.HopArcOffsetY);

        // Status bar at bottom
        _renderer.FillRect(sb, 0, ScreenH - StatusBarH, ScreenW, StatusBarH, GameRenderer.BgPanel);
        _renderer.DrawText(sb, _ctx.CurrentMapId, 8, ScreenH - StatusBarH + 6, GameRenderer.TextDim);
        string posText = $"({_ctx.PlayerX},{_ctx.PlayerY})";
        _renderer.DrawText(sb, posText, ScreenW - 80, ScreenH - StatusBarH + 6, GameRenderer.TextDim);

        // Hint
        _renderer.DrawText(sb, "ESC/ENTER: menu  WASD/arrows: move  Z: interact",
            8, ScreenH - StatusBarH + 6, GameRenderer.TextDim);

        // Debug console overlay
        if (_debug.IsOpen)
        {
            const int ConsoleH = 40;
            int consoleY = ScreenH - StatusBarH - ConsoleH;
            _renderer.FillRect(sb, 0, consoleY, ScreenW, ConsoleH, Color.Black);

            // Output line (previous command result)
            if (!string.IsNullOrEmpty(_debug.LastOutput))
                _renderer.DrawText(sb, _debug.LastOutput, 8, consoleY + 4, Color.Yellow);

            // Input line with cursor
            string inputLine = "> " + _debug.CurrentInput + "_";
            _renderer.DrawText(sb, inputLine, 8, consoleY + 20, Color.White);
        }
    }

    // ── ASCII debug map ────────────────────────────────────────────────────────
    // GBA-screen grid: 240×160 px / 16 px per tile = 15 cols × 10 rows.
    // Player (@) is centred at col 7, row 5. Out-of-bounds cells are space.

    private const int AsciiCols = 15;
    private const int AsciiRows = 10;

    // Each tile renders as 2 chars wide to compensate for monospace character aspect ratio
    // (chars are ~2× taller than wide), giving a landscape 30×10 grid for a 15×10 tile view.
    private static string CollToStr(byte c) => c switch
    {
        0x00                                => "..",  // Floor
        0x07                                => "##",  // Wall
        0x12 or 0x15                        => "TT",  // Cut/Headbutt tree
        0x14                                => "::",  // Long grass
        0x18                                => "\"\"", // Tall grass (wild encounter)
        0x23                                => "__",  // Ice
        >= 0x20 and <= 0x2F                 => "~~",  // Water / whirlpool
        >= 0x30 and <= 0x3F                 => "~~",  // Waterfall
        >= 0x70 and <= 0x7F                 => "ww",  // Warp tile
        >= 0x80 and <= 0x9F                 => "##",  // Counter / bookshelf / PC
        0xA0                                => ">>",  // HopRight
        0xA1                                => "<<",  // HopLeft
        0xA3                                => "vv",  // HopDown
        0xA4                                => "v>",  // HopDownRight
        0xA5                                => "v<",  // HopDownLeft
        >= 0xB0 and <= 0xBF                 => "||",  // Directional walls
        >= 0xC0 and <= 0xCF                 => "~~",  // Water buoys
        _                                   => "??"
    };

    private IEnumerable<string> BuildAsciiMap()
    {
        if (_ctx.Maps is null || !_ctx.Maps.TryGet(_ctx.CurrentMapId, out var map) || map is null)
        {
            yield return $"  [no map: {_ctx.CurrentMapId}]";
            yield break;
        }

        int px = _ctx.PlayerX;
        int py = _ctx.PlayerY;

        int camX = Math.Clamp(px - AsciiCols / 2, 0, Math.Max(0, map.Width  - AsciiCols));
        int camY = Math.Clamp(py - AsciiRows / 2, 0, Math.Max(0, map.Height - AsciiRows));

        int barWidth = AsciiCols * 2; // each tile = 2 chars
        yield return $"  map={_ctx.CurrentMapId} player=({px},{py}) cam=({camX},{camY}) hop={_playerController.IsHopping}";
        yield return "  +" + new string('-', barWidth) + "+";

        for (int row = 0; row < AsciiRows; row++)
        {
            var sb = new System.Text.StringBuilder("  |");
            for (int col = 0; col < AsciiCols; col++)
            {
                int mx = camX + col;
                int my = camY + row;
                if (mx < 0 || my < 0 || mx >= map.Width || my >= map.Height)
                    sb.Append("  ");
                else if (mx == px && my == py)
                    sb.Append("@@");
                else
                    sb.Append(CollToStr(map.GetCollision(mx, my)));
            }
            sb.Append('|');
            yield return sb.ToString();
        }

        yield return "  +" + new string('-', barWidth) + "+";
        yield return "  .. floor  ## wall  \"\" grass  ~~ water  vv/<</>>/v>/v< ledge  ww warp";
    }

    private void ExecuteWarp(string targetMapId, int targetWarpId)
    {
        if (!_ctx.Maps.TryGet(targetMapId, out var map) || map is null) return;

        if (targetWarpId >= 0 && targetWarpId < map.Warps.Length)
        {
            var dest = map.Warps[targetWarpId];
            _ctx.PlayerX = dest.X;
            _ctx.PlayerY = dest.Y;
        }

        _ctx.CurrentMapId  = targetMapId;
        _ctx.MapStatus     = MapStatus.Start;
        _ctx.EventsEnabled = false;

        if (!string.IsNullOrEmpty(map.MusicId))
            _audio.PlayMusic(map.MusicId);
    }
}
