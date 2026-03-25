namespace PokeCrystal.World.Systems;

using PokeCrystal.Scripting;

/// <summary>
/// Handles player interaction with NPCs and BG events (signs, bookshelves, etc.).
/// Called during the Handle phase when the player presses A facing an object.
/// Interaction is triggered externally (e.g., by input layer) via TryInteract.
/// </summary>
public sealed class MapObjectSystem : IWorldSystem
{
    private readonly ScriptEngine _scriptEngine;
    private bool _interactRequested;

    public MapObjectSystem(ScriptEngine scriptEngine)
        => _scriptEngine = scriptEngine;

    /// <summary>Called by the input/game layer to request an interaction this frame.</summary>
    public void RequestInteract() => _interactRequested = true;

    public void Update(WorldContext ctx)
    {
        if (!_interactRequested) return;
        _interactRequested = false;

        if (!ctx.EventsEnabled) return;
        if (!ctx.Maps.TryGet(ctx.CurrentMapId, out var map) || map is null) return;

        // Facing tile offset
        var (dx, dy) = ctx.Facing switch
        {
            Schema.FacingDirection.Up    => (0, -1),
            Schema.FacingDirection.Down  => (0,  1),
            Schema.FacingDirection.Left  => (-1, 0),
            Schema.FacingDirection.Right => (1,  0),
            _ => (0, 0)
        };
        int tx = ctx.PlayerX + dx;
        int ty = ctx.PlayerY + dy;

        // Check NPCs
        foreach (var npc in map.Npcs)
        {
            if (npc.X == tx && npc.Y == ty)
            {
                if (!string.IsNullOrEmpty(npc.ScriptId))
                    _scriptEngine.Start(npc.ScriptId, ctx);
                return;
            }
        }

        // Check BG events (signs)
        foreach (var bg in map.BgEvents)
        {
            if (bg.X == tx && bg.Y == ty)
            {
                if (!string.IsNullOrEmpty(bg.TextId))
                    ctx.WriteText(bg.TextId);
                return;
            }
        }
    }
}
