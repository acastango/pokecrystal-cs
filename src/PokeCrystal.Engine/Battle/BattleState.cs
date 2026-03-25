namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Schema;

/// <summary>
/// Mutable state for one combatant during a battle.
/// Wraps the immutable BattlePokemon record with mutation helpers so the
/// engine can apply damage, status inflictions, and stat-stage changes
/// without violating the record's immutability at the schema level.
/// </summary>
public sealed class CombatantState
{
    public BattlePokemon Pokemon { get; set; }
    public StatStages    Stages  { get; set; } = StatStages.Default;
    public VolatileStatus Volatile { get; set; }
    public SideCondition  Side    { get; set; }

    public int SafeguardTurns  { get; set; }
    public int ScreenTurns     { get; set; }   // Light Screen or Reflect remaining turns
    public int ToxicCounter    { get; set; }   // BadlyPoisoned: 1-based, grows each turn
    public int ConfusionTurns  { get; set; }   // remaining turns confused (0 = not confused)

    public bool IsAlive => Pokemon.Hp > 0;

    public CombatantState(BattlePokemon pokemon) => Pokemon = pokemon;

    public void TakeDamage(int amount)
        => Pokemon = Pokemon with { Hp = Math.Max(0, Pokemon.Hp - amount) };

    public void Heal(int amount)
        => Pokemon = Pokemon with { Hp = Math.Min(Pokemon.MaxHp, Pokemon.Hp + amount) };

    public void SetStatus(PrimaryStatus status, byte sleepCounter = 0)
        => Pokemon = Pokemon with { Status = status, SleepCounter = sleepCounter };

    /// <summary>Decrements the sleep counter and clears status when it hits 0.</summary>
    public void TickSleep()
    {
        if (Pokemon.Status != PrimaryStatus.Asleep) return;
        if (Pokemon.SleepCounter > 0)
            Pokemon = Pokemon with { SleepCounter = (byte)(Pokemon.SleepCounter - 1) };
        if (Pokemon.SleepCounter == 0)
            Pokemon = Pokemon with { Status = PrimaryStatus.None };
    }

    /// <summary>
    /// Clamps and applies a stat stage delta. Returns the actual delta applied
    /// (may be 0 if already at min/max).
    /// </summary>
    public int ChangeStage(StatType stat, int delta)
    {
        var s = Stages;
        int old = stat switch
        {
            StatType.Attack  => s.Attack,
            StatType.Defense => s.Defense,
            StatType.Speed   => s.Speed,
            StatType.SpAtk   => s.SpAtk,
            StatType.SpDef   => s.SpDef,
            _                => StatStages.Neutral,
        };
        int clamped = Math.Clamp(old + delta, StatStages.Min, StatStages.Max);
        Stages = stat switch
        {
            StatType.Attack  => s with { Attack  = clamped },
            StatType.Defense => s with { Defense = clamped },
            StatType.Speed   => s with { Speed   = clamped },
            StatType.SpAtk   => s with { SpAtk   = clamped },
            StatType.SpDef   => s with { SpDef   = clamped },
            _                => s,
        };
        return clamped - old;
    }
}

/// <summary>
/// Full mutable state for one in-progress battle.
/// Owns both combatant states plus field-wide conditions.
/// </summary>
public sealed class BattleState
{
    public CombatantState Player   { get; }
    public CombatantState Opponent { get; }
    public Weather Weather      { get; set; }
    public int     WeatherTurns { get; set; }
    public int     Turn         { get; set; }
    public bool    IsWild       { get; set; }
    public int     FleeAttempts { get; set; }

    public BattleState(BattlePokemon player, BattlePokemon opponent, bool isWild)
    {
        Player   = new CombatantState(player);
        Opponent = new CombatantState(opponent);
        IsWild   = isWild;
    }

    /// <summary>
    /// Returns a lightweight read-only context view for the given attacker.
    /// Used to satisfy IBattleContext for the calculator interfaces.
    /// </summary>
    public BattleContextView AsContext(bool playerIsAttacker)
        => new(this, playerIsAttacker);
}

/// <summary>
/// Read-only view of BattleState satisfying IBattleContext.
/// playerIsAttacker controls which side maps to Attacker/Defender.
/// Re-created per move execution — no state of its own.
/// </summary>
public sealed class BattleContextView : IBattleContext
{
    private readonly BattleState _state;
    private readonly bool        _playerIsAttacker;

    public BattleContextView(BattleState state, bool playerIsAttacker)
    {
        _state            = state;
        _playerIsAttacker = playerIsAttacker;
    }

    private CombatantState Atk => _playerIsAttacker ? _state.Player : _state.Opponent;
    private CombatantState Def => _playerIsAttacker ? _state.Opponent : _state.Player;

    public BattlePokemon  Attacker              => Atk.Pokemon;
    public BattlePokemon  Defender              => Def.Pokemon;
    public StatStages     AttackerStages        => Atk.Stages;
    public StatStages     DefenderStages        => Def.Stages;
    public VolatileStatus AttackerVolatile      => Atk.Volatile;
    public VolatileStatus DefenderVolatile      => Def.Volatile;
    public SideCondition  AttackerSide          => Atk.Side;
    public SideCondition  DefenderSide          => Def.Side;
    public int            AttackerSafeguardTurns => Atk.SafeguardTurns;
    public int            DefenderSafeguardTurns => Def.SafeguardTurns;
    public int            AttackerScreenTurns   => Atk.ScreenTurns;
    public int            DefenderScreenTurns   => Def.ScreenTurns;
    public Weather        Weather               => _state.Weather;
    public int            Turn                  => _state.Turn;
    public bool           IsWild                => _state.IsWild;
}
