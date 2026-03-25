namespace PokeCrystal.Schema;

/// <summary>
/// Read-only view of battle state passed to mechanic interfaces.
/// Defined here in Schema so all interfaces share the same type.
/// L2 (Core Engine) provides the concrete implementation.
/// </summary>
public interface IBattleContext
{
    BattlePokemon Attacker { get; }
    BattlePokemon Defender { get; }
    StatStages AttackerStages { get; }
    StatStages DefenderStages { get; }
    VolatileStatus AttackerVolatile { get; }
    VolatileStatus DefenderVolatile { get; }
    SideCondition AttackerSide { get; }
    SideCondition DefenderSide { get; }
    int AttackerSafeguardTurns { get; }
    int DefenderSafeguardTurns { get; }
    int AttackerScreenTurns { get; }    // Light Screen / Reflect remaining
    int DefenderScreenTurns { get; }
    Weather Weather { get; }
    int Turn { get; }
    bool IsWild { get; }
}
