namespace PokeCrystal.Schema;

/// <summary>
/// Transient battle copy of a Pokémon — mirrors battle_struct from macros/ram.asm.
/// Created at battle start from PartyPokemon; never written back to party directly
/// (the engine syncs HP and status at battle end). Types can change via Conversion.
/// </summary>
public record BattlePokemon(
    string SpeciesId,
    string HeldItemId,
    string[] Moves,           // 4 move ID strings
    DVs DVs,
    byte[] PP,                // 4 bytes — current PP
    byte Happiness,
    byte Level,
    PrimaryStatus Status,
    byte SleepCounter,
    int Hp,
    int MaxHp,
    int Attack,
    int Defense,
    int Speed,
    int SpAtk,
    int SpDef,
    string Type1Id,           // can differ from species after Conversion
    string Type2Id
);
