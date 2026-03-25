namespace PokeCrystal.Schema;

/// <summary>
/// Static move definition — immutable, loaded from data/moves/.
/// Effect key references IMoveEffect implementations in L2.
/// Priority and MoveFlags are extractor-computed (not stored in ASM data tables).
/// </summary>
public record MoveData(
    string Id,
    string Name,
    int Power,           // 0 for status moves
    string TypeId,
    int Accuracy,        // 0-100; 0 = never misses
    int PP,
    int EffectChance,    // 0 = no secondary effect; otherwise percent chance
    string EffectKey,
    int Priority,
    MoveFlags Flags,
    MoveTarget Target
) : IIdentifiable;

[Flags]
public enum MoveFlags
{
    None    = 0,
    Contact = 1 << 0,
    Sound   = 1 << 1,
    Punch   = 1 << 2,
}

public enum MoveTarget
{
    SelectedOpponent,
    AllOpponents,
    RandomOpponent,
    Self,
    AllAllies,
    Battlefield,
}
