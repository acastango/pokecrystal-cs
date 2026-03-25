namespace PokeCrystal.Schema;

/// <summary>
/// Volatile (transient, battle-only) status conditions — from the five SubStatus
/// bytes per combatant in battle_constants.asm. Flags are not persisted to SRAM.
/// Bits 0-7 = SubStatus1, 8-15 = SubStatus2, 16-23 = SubStatus3,
/// 24-31 = SubStatus4, 32-39 = SubStatus5.
/// </summary>
[Flags]
public enum VolatileStatus : ulong
{
    None          = 0,

    // SubStatus1
    Nightmare     = 1UL << 0,
    Curse         = 1UL << 1,
    Protect       = 1UL << 2,
    Identified    = 1UL << 3,   // Foresight
    Perish        = 1UL << 4,
    Endure        = 1UL << 5,
    Rollout       = 1UL << 6,
    Infatuated    = 1UL << 7,

    // SubStatus2
    Curled        = 1UL << 8,   // used by Rollout/Defense Curl

    // SubStatus3
    Bide          = 1UL << 16,
    Rampage       = 1UL << 17,  // Thrash/Petal Dance/Outrage
    InLoop        = 1UL << 18,  // executing a multi-turn move
    Flinched      = 1UL << 19,
    Charged       = 1UL << 20,  // SolarBeam charge, Skull Bash, etc.
    Underground   = 1UL << 21,  // Dig
    Flying        = 1UL << 22,  // Fly
    Confused      = 1UL << 23,

    // SubStatus4
    XAccuracy     = 1UL << 24,
    Mist          = 1UL << 25,
    FocusEnergy   = 1UL << 26,
    Substitute    = 1UL << 28,
    Recharge      = 1UL << 29,  // must recharge next turn (Hyper Beam)
    Rage          = 1UL << 30,
    LeechSeed     = 1UL << 31,

    // SubStatus5
    Toxic         = 1UL << 32,  // badly poisoned counter active
    Transformed   = 1UL << 35,
    Encored       = 1UL << 36,
    LockOn        = 1UL << 37,
    DestinyBond   = 1UL << 38,
    CantRun       = 1UL << 39,  // Mean Look / trapping
}

/// <summary>Weather affecting all Pokémon on the field. Not persisted.</summary>
public enum Weather { None, Rain, Sun, Sandstorm }

/// <summary>
/// Per-side field conditions (Spikes, Safeguard, screens).
/// Screens track their own turn counters in BattleContext.
/// </summary>
[Flags]
public enum SideCondition
{
    None        = 0,
    Spikes      = 1 << 0,
    Safeguard   = 1 << 1,
    LightScreen = 1 << 2,
    Reflect     = 1 << 3,
}
