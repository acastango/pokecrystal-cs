namespace PokeCrystal.World.Systems;

using PokeCrystal.Scripting;

/// <summary>
/// Fires coord events (step triggers) when the player steps onto a matching tile.
/// Mirrors Crystal's CheckCoordEvents routine.
/// </summary>
public sealed class CoordEventSystem : IWorldSystem
{
    private readonly ScriptEngine _scriptEngine;

    public CoordEventSystem(ScriptEngine scriptEngine)
        => _scriptEngine = scriptEngine;

    public void Update(WorldContext ctx)
    {
        if (!ctx.EventsEnabled) return;
        if (!ctx.Maps.TryGet(ctx.CurrentMapId, out var map) || map is null) return;

        foreach (var ev in map.CoordEvents)
        {
            if (ev.X == ctx.PlayerX && ev.Y == ctx.PlayerY)
            {
                _scriptEngine.Start(ev.ScriptId, ctx);
                return;
            }
        }
    }
}
