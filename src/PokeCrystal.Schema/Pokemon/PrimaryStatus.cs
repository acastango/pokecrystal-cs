namespace PokeCrystal.Schema;

/// <summary>
/// Primary (persistent) status condition. Stored in the status byte of
/// StoredPokemon/PartyPokemon. A Pokémon can have at most one primary status.
/// Asleep stores a sleep counter in bits 0-2 of the same byte.
/// </summary>
public enum PrimaryStatus
{
    None          = 0,
    Asleep        = 1,  // bits 0-2 = remaining sleep turns
    Poisoned      = 2,
    Burned        = 3,
    Frozen        = 4,
    Paralyzed     = 5,
    BadlyPoisoned = 6,  // toxic — bit 7 in the ASM status byte
}

/// <summary>Gender of a Pokémon or trainer.</summary>
public enum Gender { Male, Female, Genderless }

/// <summary>Time of day as used by the GBC RTC and encounter/event tables.</summary>
public enum TimeOfDay { Morning, Day, Evening, Night }

/// <summary>Growth rate determining how much EXP is needed per level.</summary>
public enum GrowthRate { MediumFast, Erratic, Fluctuating, MediumSlow, Fast, Slow }

/// <summary>Battle stat types, used for stat-stage indexing and IStatCalculator.</summary>
public enum StatType { Hp, Attack, Defense, Speed, SpAtk, SpDef }
