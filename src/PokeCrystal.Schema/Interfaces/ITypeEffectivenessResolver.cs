namespace PokeCrystal.Schema;

/// <summary>
/// Resolves type matchup multipliers. L2 provides the Gen 2 table lookup.
/// Mods can replace to add new types or custom immunity rules.
/// </summary>
public interface ITypeEffectivenessResolver
{
    /// <summary>
    /// Returns the combined effectiveness multiplier for an attack of attackType
    /// against a defender with defType1 and defType2.
    /// Returns 0, 0.5, 1, 2, or 4 (double-weakness).
    /// </summary>
    float GetMultiplier(string attackTypeId, string defType1Id, string defType2Id,
                        IBattleContext ctx);
}
