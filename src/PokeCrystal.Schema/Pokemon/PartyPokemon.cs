namespace PokeCrystal.Schema;

/// <summary>
/// Pokémon in the active party — mirrors party_struct from macros/ram.asm.
/// Extends StoredPokemon with computed stats, current HP, and primary status.
/// Stats are recalculated on level-up, evolution, and box retrieve.
/// </summary>
public record PartyPokemon(
    StoredPokemon Base,
    PrimaryStatus Status,
    byte SleepCounter,        // remaining sleep turns (0 when not asleep)
    int CurrentHp,
    int MaxHp,
    int Attack,
    int Defense,
    int Speed,
    int SpAtk,
    int SpDef
);
