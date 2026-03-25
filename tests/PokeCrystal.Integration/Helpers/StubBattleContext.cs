namespace PokeCrystal.Integration.Helpers;

using PokeCrystal.Schema;

/// <summary>
/// Minimal IBattleContext for type effectiveness tests.
/// All fields neutral; no Foresight active.
/// </summary>
public sealed class StubBattleContext : IBattleContext
{
    public static readonly StubBattleContext Default = new();

    private static readonly BattlePokemon _stub = new(
        SpeciesId: "NONE", HeldItemId: "NO_ITEM",
        Moves: [], DVs: new(0, 0, 0, 0), PP: [],
        Happiness: 0, Level: 1,
        Status: PrimaryStatus.None,
        SleepCounter: 0,
        Hp: 1, MaxHp: 1,
        Attack: 1, Defense: 1, Speed: 1, SpAtk: 1, SpDef: 1,
        Type1Id: "NORMAL", Type2Id: "NORMAL");

    public BattlePokemon Attacker => _stub;
    public BattlePokemon Defender => _stub;
    public StatStages AttackerStages => StatStages.Default;
    public StatStages DefenderStages => StatStages.Default;
    public VolatileStatus AttackerVolatile => VolatileStatus.None;
    public VolatileStatus DefenderVolatile => VolatileStatus.None;
    public SideCondition AttackerSide => SideCondition.None;
    public SideCondition DefenderSide => SideCondition.None;
    public int AttackerSafeguardTurns => 0;
    public int DefenderSafeguardTurns => 0;
    public int AttackerScreenTurns => 0;
    public int DefenderScreenTurns => 0;
    public Weather Weather => Weather.None;
    public int Turn => 1;
    public bool IsWild => false;
}
