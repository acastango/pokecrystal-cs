namespace PokeCrystal.Schema;

/// <summary>
/// Pokémon in PC storage or save file — mirrors box_struct from macros/ram.asm.
/// All IDs are string keys resolved against the data registry at load time.
/// CaughtData (time-of-day, level, gender, location) is packed in 2 bytes in ASM;
/// we expand it here for clarity.
/// </summary>
public record StoredPokemon(
    string SpeciesId,
    string HeldItemId,
    string[] Moves,           // exactly 4 move ID strings
    ushort TrainerId,
    int Exp,
    StatExp StatExp,
    DVs DVs,
    byte[] PP,                // 4 bytes — current PP per move slot
    byte Happiness,
    byte PokerusStatus,       // bits: infected (0x0F) + cured (0xF0)
    TimeOfDay CaughtTimeOfDay,
    byte CaughtLevel,
    Gender CaughtGender,
    string CaughtLocationId,
    byte Level
);
