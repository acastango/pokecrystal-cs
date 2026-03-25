namespace PokeCrystal.Schema;

/// <summary>
/// Static species definition — immutable, loaded from data/pokemon/.
/// All ID references are string keys resolved by the data registry.
/// TmHmMoves is a set of move IDs this species can learn via TM/HM —
/// stored as a list for JSON compatibility, queried by Contains in L2.
/// </summary>
public record SpeciesData(
    string Id,
    string Name,
    int BaseHp,
    int BaseAttack,
    int BaseDefense,
    int BaseSpeed,
    int BaseSpAtk,
    int BaseSpDef,
    string Type1Id,
    string Type2Id,
    GrowthRate GrowthRate,
    string[] EggGroups,
    float GenderRatio,
    int CatchRate,
    int BaseExp,
    int HatchCycles,
    string[] TmHmMoves,       // move IDs learnable by TM/HM — replaces bool[] TmHmFlags
    LearnsetEntry[] Learnset,
    EvolutionEntry[] Evolutions,
    string[] EggMoves,
    string SpriteId,
    string CryId
) : IIdentifiable;

public record LearnsetEntry(byte Level, string MoveId);

/// <summary>
/// Param is a raw string — numeric for Level/Stat evolutions (e.g. "16"),
/// item/condition ID for Item/Trade evolutions (e.g. "MOON_STONE").
/// </summary>
public record EvolutionEntry(EvolutionMethod Method, string Param, string TargetSpeciesId);

public enum EvolutionMethod
{
    Level,
    LevelAtTime,
    Item,
    Trade,
    TradeWithItem,
    Happiness,
    HappinessAtTime,
    Stat,             // Tyrogue: Attack DV vs Defense DV
}
