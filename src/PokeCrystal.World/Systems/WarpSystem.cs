namespace PokeCrystal.World.Systems;

/// <summary>
/// Checks whether the player is standing on a warp tile and queues a transition.
/// Mirrors Crystal's CheckWarps routine.
/// </summary>
public sealed class WarpSystem : IWorldSystem
{
    public void Update(WorldContext ctx)
    {
        if (!ctx.EventsEnabled) return;
        if (!ctx.Maps.TryGet(ctx.CurrentMapId, out var map) || map is null) return;

        foreach (var warp in map.Warps)
        {
            if (warp.X == ctx.PlayerX && warp.Y == ctx.PlayerY)
            {
                ctx.Warp(warp.TargetMapId, warp.TargetWarpId);
                return;
            }
        }
    }
}
