namespace PokeCrystal.World.Systems;

using PokeCrystal.Schema;

/// <summary>
/// Updates WorldContext.CurrentTimeOfDay each frame from the wall clock.
/// Crystal uses Morning=5-9, Day=9-18, Evening=18-21, Night=21-5.
/// </summary>
public sealed class TimeSystem : IWorldSystem
{
    public void Update(WorldContext ctx)
    {
        var hour = DateTime.Now.Hour;
        ctx.CurrentTimeOfDay = hour switch
        {
            >= 5 and < 10  => TimeOfDay.Morning,
            >= 10 and < 18 => TimeOfDay.Day,
            >= 18 and < 21 => TimeOfDay.Evening,
            _              => TimeOfDay.Night,
        };
    }
}
