namespace PokeCrystal.World.Systems;

using PokeCrystal.Schema;

/// <summary>
/// Applies queued player movement to WorldContext with Crystal-accurate smooth scrolling.
///
/// Crystal StepVectors (map_objects.asm): normal walk = 8 frames/step, 2 GB px/frame = 16 GB px total.
/// Our tile unit = 1 block = 16 GB px.  Sub-tile offset is in normalised [-1,+1] range;
/// multiply by TileSize (48 screen px) in the renderer.
///
/// Flow (mirrors Crystal DoPlayerMovement / AddStepVector):
///   1. Input arrives → validate destination, commit PlayerX/Y immediately, begin step.
///   2. Each frame during step: advance SubTileOffset toward 0.
///   3. Step completes → SubTileOffset = 0, check for buffered input.
/// </summary>
public sealed class PlayerController : IWorldSystem
{
    /// <summary>Set by the game layer each frame before Tick().</summary>
    public FacingDirection? PendingDirection { get; set; }

    // Crystal StepVectors: STEP_WALK normal = 8 frames × 2 GB px/frame = 16 GB px/step.
    // We run at 60 fps on PC (vs ~60 on GBC), but use 16 frames so the leg animation
    // is clearly visible before cycling. OffsetPerFrame recalculates automatically.
    private const int StepUnits    = 1;
    private const int WalkFrames   = 14;
    private const float OffsetPerFrame = (float)StepUnits / WalkFrames;

    // Ledge hop: 2 tiles in 16 frames (matches Crystal's hop_step2 timing).
    // The hop triggers from the player's CURRENT tile (not the destination):
    //   1. Player walks normally ONTO the ledge tile (1 normal step).
    //   2. From the ledge tile, pressing the matching direction hops 2 tiles forward,
    //      skipping the cliff-face (Wall) quarter and landing on the floor beyond.
    private const int   HopUnits         = 2;
    private const int   HopFrames        = 30;
    private const float HopOffsetPerFrame = (float)HopUnits / HopFrames; // 0.05

    // Y arc in screen pixels, sampled once per frame (Crystal hop_y_displacement × 3 scale).
    // Negative = upward. Indexed 0..15 (frame 0 = first advance after hop starts).
    private static readonly int[] s_hopArc =
        { -12, -18, -24, -30, -33, -36, -36, -36, -33, -30, -27, -24, -18, -12, 0, 0 };

    private int   _stepFramesLeft;
    private float _stepDxNorm; // normalised step direction (-1, 0, or +1)
    private float _stepDyNorm;
    private bool  _isHopping;

    /// <summary>
    /// Sub-tile scroll offset in normalised tile units.
    /// Multiply by TileSize (48 px) in the renderer for screen pixels.
    /// 0 = player is exactly on their logical tile.
    /// During a hop the range is [-2, +2] (two tiles).
    /// </summary>
    public float SubTileOffsetX { get; private set; }
    public float SubTileOffsetY { get; private set; }

    /// <summary>
    /// Additional vertical offset in screen pixels for the hop arc.
    /// Negative = sprite moves up. Zero when not hopping.
    /// Apply only to the player sprite — not to the camera.
    /// </summary>
    public int HopArcOffsetY { get; private set; }

    public bool IsStepping  => _stepFramesLeft > 0;
    public bool IsHopping   => _isHopping;

    /// <summary>
    /// Optional logger — set by the game layer to capture movement diagnostics.
    /// Receives one-line strings describing each step/hop attempt and outcome.
    /// </summary>
    public Action<string>? Logger { get; set; }

    /// <summary>
    /// Flips each time a new step begins.
    /// Used by the renderer to alternate walk frames (FacingStepDown1 vs FacingStepDown3).
    /// Mirrors Crystal's odd/even step tracking for foot alternation.
    /// </summary>
    public bool StepParity { get; private set; }

    public void Update(WorldContext ctx)
    {
        if (ctx.Mode != Scripting.ScriptMode.End) return; // blocked by script

        // ── Advance the current step ───────────────────────────────────────
        if (_stepFramesLeft > 0)
        {
            if (_isHopping)
            {
                // Scale frame index across the 16-entry arc table regardless of HopFrames
                int frameIdx = HopFrames - _stepFramesLeft; // 0 on first advance frame
                int arcIdx   = frameIdx * (s_hopArc.Length - 1) / (HopFrames - 1);
                SubTileOffsetX += _stepDxNorm * HopOffsetPerFrame;
                SubTileOffsetY += _stepDyNorm * HopOffsetPerFrame;
                HopArcOffsetY   = s_hopArc[arcIdx];
            }
            else
            {
                SubTileOffsetX += _stepDxNorm * OffsetPerFrame;
                SubTileOffsetY += _stepDyNorm * OffsetPerFrame;
            }

            _stepFramesLeft--;

            if (_stepFramesLeft == 0)
            {
                SubTileOffsetX = 0f;
                SubTileOffsetY = 0f;
                HopArcOffsetY  = 0;
                _isHopping     = false;
            }

            // Always yield here — new step starts next frame.
            // This gives exactly 1 frame of IsStepping=false between steps,
            // matching Crystal's per-frame engine where step-end and step-start
            // are on separate vblanks. That 1-frame gap is what shows the
            // standing pose and drives the left/right leg animation.
            return;
        }

        // ── Try to start a new step ────────────────────────────────────────
        var dir = PendingDirection;
        PendingDirection = null;
        if (dir is null) return;

        ctx.Facing = dir.Value;

        var (dx, dy) = dir.Value switch
        {
            FacingDirection.Up    => (0, -1),
            FacingDirection.Down  => (0,  1),
            FacingDirection.Left  => (-1, 0),
            FacingDirection.Right => (1,  0),
            _ => (0, 0)
        };

        if (!ctx.Maps.TryGet(ctx.CurrentMapId, out var map) || map is null) return;

        int nx = ctx.PlayerX + dx;
        int ny = ctx.PlayerY + dy;

        // Out-of-bounds → connection exit
        if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height)
        {
            TryConnection(ctx, map, dir.Value, nx, ny);
            return;
        }

        // ── Ledge hop (triggered from current tile) ──────────────────────────
        // Crystal: player walks onto the ledge tile normally, then hops 2 tiles
        // forward in the matching direction, clearing the cliff-face (Wall) quarter.
        byte curColl = map.GetCollision(ctx.PlayerX, ctx.PlayerY);
        byte dstColl = map.GetCollision(nx, ny);
        Logger?.Invoke($"[TRY] ({ctx.PlayerX},{ctx.PlayerY}) dir={dir.Value} " +
                       $"curColl=0x{curColl:X2} dst=({nx},{ny}) dstColl=0x{dstColl:X2}");

        if ((curColl & 0xF0) == 0xA0 && CollisionConstants.IsLedgeCrossable(curColl, dir.Value))
        {
            // Landing is 2 tiles ahead from the current (ledge) position
            int lx = ctx.PlayerX + dx * 2;
            int ly = ctx.PlayerY + dy * 2;
            byte landColl = (lx >= 0 && ly >= 0 && lx < map.Width && ly < map.Height)
                ? map.GetCollision(lx, ly) : (byte)0xFF;
            bool oob      = lx < 0 || ly < 0 || lx >= map.Width || ly >= map.Height;
            bool blocked  = !oob && !CollisionConstants.IsLandWalkable(landColl);

            Logger?.Invoke($"[HOP?] curColl=0x{curColl:X2} landing=({lx},{ly}) " +
                           $"landColl=0x{landColl:X2} oob={oob} blocked={blocked}");

            if (oob || blocked) return;

            ctx.PlayerX     = lx;
            ctx.PlayerY     = ly;
            StepParity      = !StepParity;
            _stepDxNorm     = dx;
            _stepDyNorm     = dy;
            SubTileOffsetX  = -(float)(dx * HopUnits);
            SubTileOffsetY  = -(float)(dy * HopUnits);
            _stepFramesLeft = HopFrames;
            _isHopping      = true;
            Logger?.Invoke($"[HOP!] → ({lx},{ly}) SubTileOffset=({SubTileOffsetX},{SubTileOffsetY})");
            return;
        }

        // ── Normal collision check ────────────────────────────────────────────
        if (!CollisionConstants.IsLandWalkable(dstColl))
        {
            Logger?.Invoke($"[BLOCKED] dst=({nx},{ny}) dstColl=0x{dstColl:X2}");
            return;
        }

        // Commit destination immediately; scroll starts from source visually
        ctx.PlayerX = nx;
        ctx.PlayerY = ny;
        Logger?.Invoke($"[STEP] → ({nx},{ny})");

        StepParity      = !StepParity; // alternate foot each step (FacingStepx1 ↔ FacingStepx3)
        _stepDxNorm     = dx;
        _stepDyNorm     = dy;
        SubTileOffsetX  = -dx;
        SubTileOffsetY  = -dy;
        _stepFramesLeft = WalkFrames;
    }

    // ── Map connection (EnterMapConnection) ───────────────────────────────

    private static void TryConnection(
        WorldContext ctx, MapData current, FacingDirection dir, int nx, int ny)
    {
        string dirStr = dir switch
        {
            FacingDirection.Up    => "north",
            FacingDirection.Down  => "south",
            FacingDirection.Left  => "west",
            FacingDirection.Right => "east",
            _ => ""
        };

        MapConnection? conn = null;
        foreach (var c in current.Connections)
        {
            if (string.Equals(c.Direction, dirStr, StringComparison.OrdinalIgnoreCase))
            { conn = c; break; }
        }
        if (conn is null) return;

        if (!ctx.Maps.TryGet(conn.TargetMapId, out var target) || target is null) return;

        int entryX, entryY;
        switch (dirStr)
        {
            case "north":
                entryY = target.Height - 1;
                entryX = Math.Clamp(ctx.PlayerX + conn.Offset, 0, target.Width - 1);
                break;
            case "south":
                entryY = 0;
                entryX = Math.Clamp(ctx.PlayerX + conn.Offset, 0, target.Width - 1);
                break;
            case "west":
                entryX = target.Width - 1;
                entryY = Math.Clamp(ctx.PlayerY + conn.Offset, 0, target.Height - 1);
                break;
            default: // east
                entryX = 0;
                entryY = Math.Clamp(ctx.PlayerY + conn.Offset, 0, target.Height - 1);
                break;
        }

        ctx.CurrentMapId  = conn.TargetMapId;
        ctx.PlayerX       = entryX;
        ctx.PlayerY       = entryY;
        ctx.MapStatus     = MapStatus.Start;
        ctx.EventsEnabled = false;
    }
}
