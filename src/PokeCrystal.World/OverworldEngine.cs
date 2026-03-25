namespace PokeCrystal.World;

using PokeCrystal.Scripting;
using PokeCrystal.World.Systems;

/// <summary>
/// Drives the overworld frame loop. Owned by the game layer (L6).
/// Call Tick(ctx) once per frame. MapStatus mirrors Crystal's MAPSTATUS_* constants:
///   Start  — first frame after load; run entry script if present
///   Enter  — transition animation (delegated to L6)
///   Handle — normal per-frame update; run all systems
///   Done   — warp or battle pending; L6 consumes PendingWarpMapId / PendingBattle
/// </summary>
public sealed class OverworldEngine
{
    private readonly ScriptEngine _scriptEngine;
    private readonly IReadOnlyList<IWorldSystem> _systems;

    public OverworldEngine(ScriptEngine scriptEngine, IEnumerable<IWorldSystem> systems)
    {
        _scriptEngine = scriptEngine;
        _systems = [.. systems];
    }

    public void Tick(WorldContext ctx)
    {
        switch (ctx.MapStatus)
        {
            case MapStatus.Start:
                ctx.MapStatus = MapStatus.Enter;
                if (!string.IsNullOrEmpty(ctx.Maps.TryGet(ctx.CurrentMapId, out var map)
                        ? map?.MapScriptId : null)
                    && map!.MapScriptId is { } entryScript)
                {
                    _scriptEngine.Start(entryScript, ctx);
                }
                break;

            case MapStatus.Enter:
                // Entry animation handled by L6 rendering; advance immediately
                ctx.MapStatus = MapStatus.Handle;
                ctx.EventsEnabled = true;
                break;

            case MapStatus.Handle:
                TickHandle(ctx);
                break;

            case MapStatus.Done:
                // L6 reads PendingWarpMapId / PendingBattle and transitions
                break;
        }
    }

    private void TickHandle(WorldContext ctx)
    {
        // Advance running script if any
        if (ctx.Mode != ScriptMode.End)
        {
            _scriptEngine.Run(ctx);
            return; // systems blocked while script is running
        }

        foreach (var system in _systems)
            system.Update(ctx);
    }
}
