namespace PokeCrystal.World.Systems;

using PokeCrystal.Schema;
using PokeCrystal.Scripting;

/// <summary>
/// Rolls for wild encounters when the player steps in grass or water.
/// Mirrors Crystal's TryWildEncounter. Rate is out of 256.
/// Decrement cooldown each frame; roll only when cooldown hits 0.
/// </summary>
public sealed class WildEncounterSystem : IWorldSystem
{
    private const int CooldownFrames = 90; // ~1.5s grace period between encounters

    public void Update(WorldContext ctx)
    {
        if (!ctx.EventsEnabled) return;
        if (ctx.WildEncountersDisabled) return;
        if (!ctx.Maps.TryGet(ctx.CurrentMapId, out var map) || map is null) return;

        // Only trigger in tall grass (mirrors Crystal's DoWildEncounters grass check)
        if (!CollisionConstants.IsTallGrass(map.GetCollision(ctx.PlayerX, ctx.PlayerY))) return;

        if (ctx.WildEncounterCooldown > 0)
        {
            ctx.WildEncounterCooldown--;
            return;
        }

        var (slot, rate) = PickSlot(map, ctx.CurrentTimeOfDay);
        if (slot is null || rate == 0) return;

        if (Random.Shared.Next(256) >= rate) return;

        ctx.LoadWildMon(slot.SpeciesId, slot.Level);
        ctx.StartBattle();
        ctx.WildEncounterCooldown = CooldownFrames;
    }

    private static (WildSlot? slot, int rate) PickSlot(MapData map, TimeOfDay tod)
    {
        if (map.WildGrass is { } grass)
        {
            var (slots, rate) = tod switch
            {
                TimeOfDay.Morning => (grass.Morn, grass.MornRate),
                TimeOfDay.Day     => (grass.Day,  grass.DayRate),
                _                 => (grass.Nite, grass.NiteRate),
            };
            if (slots.Length > 0)
                return (slots[Random.Shared.Next(slots.Length)], rate);
        }
        if (map.WildWater is { } water && water.Slots.Length > 0)
            return (water.Slots[Random.Shared.Next(water.Slots.Length)], water.Rate);

        return (null, 0);
    }
}
