namespace PokeCrystal.World.Systems;

/// <summary>
/// A system that runs once per overworld frame during the Handle phase.
/// Systems are called in registration order by OverworldEngine.
/// </summary>
public interface IWorldSystem
{
    void Update(WorldContext ctx);
}
