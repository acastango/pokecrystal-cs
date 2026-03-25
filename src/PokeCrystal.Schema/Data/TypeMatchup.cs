namespace PokeCrystal.Schema;

/// <summary>
/// Single type effectiveness entry. Multiplier: 0, 0.5, 1, or 2.
/// Unlisted pairs default to 1x. Immunity entries (0x) are added explicitly
/// by the extractor since the ASM table omits them.
/// Id is a composite key "ATTACKER:DEFENDER" for registry lookup.
/// </summary>
public record TypeMatchup(string AttackerTypeId, string DefenderTypeId, float Multiplier)
    : IIdentifiable
{
    public string Id => $"{AttackerTypeId}:{DefenderTypeId}";
}
