namespace PokeCrystal.Schema;

/// <summary>
/// Static item definition — immutable, loaded from data/items/.
/// Price -1 = unsellable (normalised from ASM sentinel $9999).
/// </summary>
public record ItemData(
    string Id,
    string Name,
    ItemPocket Pocket,
    int Price,
    ItemFlags Flags,
    string EffectKey,
    int FlingPower
) : IIdentifiable;

public enum ItemPocket { Items, KeyItems, Balls, PcItems }

[Flags]
public enum ItemFlags
{
    None            = 0,
    Usable          = 1 << 0,
    UsableInBattle  = 1 << 1,
    UsableOnPokemon = 1 << 2,
    Holdable        = 1 << 3,
    Sellable        = 1 << 4,
}
