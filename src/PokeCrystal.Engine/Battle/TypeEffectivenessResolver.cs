namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Data;
using PokeCrystal.Schema;

/// <summary>
/// Gen 2 type effectiveness resolver.
/// TypeMatchup.Multiplier is already a float: 0=immune, 0.5=not very, 1=normal, 2=super.
/// Foresight: Normal/Fighting vs Ghost — immunity becomes 1× while Foresight is active.
/// Source: engine/battle/effect_commands.asm CheckTypeMatchup
/// </summary>
public sealed class TypeEffectivenessResolver : ITypeEffectivenessResolver
{
    private readonly IDataRegistry _registry;

    public TypeEffectivenessResolver(IDataRegistry registry) => _registry = registry;

    public float GetMultiplier(string attackTypeId, string defType1Id, string defType2Id,
        IBattleContext ctx)
    {
        bool foresight = ctx.DefenderVolatile.HasFlag(VolatileStatus.Identified);

        float e1 = GetSingleMatchup(attackTypeId, defType1Id, foresight);
        if (e1 == 0f) return 0f;

        float e2 = defType2Id == defType1Id
            ? 1f
            : GetSingleMatchup(attackTypeId, defType2Id, foresight);
        if (e2 == 0f) return 0f;

        return e1 * e2;
    }

    private float GetSingleMatchup(string attackTypeId, string defTypeId, bool foresightActive)
    {
        string key = $"{attackTypeId}:{defTypeId}";
        if (!_registry.TryGet<TypeMatchup>(key, out var matchup))
            return 1f;

        float mult = matchup!.Multiplier;

        // Foresight removes Ghost's immunity to Normal and Fighting
        if (mult == 0f && foresightActive && defTypeId == "GHOST"
            && (attackTypeId == "NORMAL" || attackTypeId == "FIGHTING"))
            return 1f;

        return mult;
    }
}
