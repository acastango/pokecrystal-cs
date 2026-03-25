namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Schema;

/// <summary>
/// Ordered log of what happened in a turn.
/// The scene layer (L13/L12) iterates this list to drive animations,
/// text boxes, and HP bar updates without re-running game logic.
/// </summary>
public abstract record BattleEvent;

/// <summary>A combatant announced a move.</summary>
public record MoveUsedEvent(bool ByPlayer, string MoveName) : BattleEvent;

/// <summary>The move missed the target.</summary>
public record MoveMissedEvent(bool ByPlayer) : BattleEvent;

/// <summary>Damage was dealt. Effectiveness: 0=immune, 0.5=NVE, 1=normal, 2=SE, 4=double-SE.</summary>
public record DamageDealtEvent(bool ToPlayer, int Amount, bool IsCritical, float Effectiveness) : BattleEvent;

/// <summary>HP was restored (Rest, Recover, Leech Seed heal, etc.).</summary>
public record HealedEvent(bool ToPlayer, int Amount) : BattleEvent;

/// <summary>A primary status condition was inflicted.</summary>
public record StatusInflictedEvent(bool ToPlayer, PrimaryStatus Status) : BattleEvent;

/// <summary>A primary status condition ended (woke up, thawed, etc.).</summary>
public record StatusCuredEvent(bool ToPlayer, PrimaryStatus WasStatus) : BattleEvent;

/// <summary>A combat stat stage changed (+1 Atk, -2 Def, etc.).</summary>
public record StatStageChangedEvent(bool ToPlayer, StatType Stat, int Delta) : BattleEvent;

/// <summary>A volatile status flag was applied (Confused, LeechSeed, etc.).</summary>
public record VolatileAppliedEvent(bool ToPlayer, VolatileStatus Flag) : BattleEvent;

/// <summary>A combatant fainted (HP reached 0).</summary>
public record FaintedEvent(bool IsPlayer) : BattleEvent;

/// <summary>End-of-turn chip damage: burn, poison, sandstorm, leechseed, recoil, confusion.</summary>
public record EndOfTurnDamageEvent(bool ToPlayer, int Amount, string Source) : BattleEvent;

/// <summary>Flee attempt succeeded.</summary>
public record FledEvent : BattleEvent;

/// <summary>Flee attempt failed (trainer battle or bad luck roll).</summary>
public record FleeFailed : BattleEvent;
