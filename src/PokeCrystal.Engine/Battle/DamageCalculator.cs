namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Schema;

/// <summary>
/// Gen 2 damage formula:
///   base = ((level * 2 / 5 + 2) * power * atk / def / 50) + 2
/// Modifiers: STAB ×1.5 (truncated), type effectiveness (float), critical ×2, item boost.
/// Selfdestruct/Explosion: uses EFFECT_EXPLOSION effect key — defense halved (min 1) before formula.
/// Source: engine/battle/effect_commands.asm CalculateDamage
/// </summary>
public sealed class DamageCalculator : IDamageCalculator
{
    private const string EffectExplosion = "EFFECT_EXPLOSION";

    private readonly ITypeEffectivenessResolver _typeResolver;

    public DamageCalculator(ITypeEffectivenessResolver typeResolver)
        => _typeResolver = typeResolver;

    public int Calculate(IBattleContext ctx, BattlePokemon attacker, BattlePokemon defender,
        MoveData move, bool isCritical)
    {
        if (move.Power == 0) return 0;

        // Critical hit: bypass stat stages (use base stats, not modified)
        int atk = isCritical
            ? GetBaseStatForCrit(ctx, attacker, move)
            : GetModifiedAtk(ctx, attacker, move);
        int def = isCritical
            ? GetBaseDefForCrit(ctx, defender, move)
            : GetModifiedDef(ctx, defender, move);

        // Selfdestruct/Explosion: halve defense (min 1)
        if (move.EffectKey == EffectExplosion)
            def = Math.Max(1, def / 2);

        int damage = (attacker.Level * 2 / 5 + 2) * move.Power * atk / def / 50 + 2;

        // STAB: ×3/2 (integer truncation)
        if (attacker.Type1Id == move.TypeId || attacker.Type2Id == move.TypeId)
            damage = damage * 3 / 2;

        // Type effectiveness
        float effectiveness = _typeResolver.GetMultiplier(
            move.TypeId, defender.Type1Id, defender.Type2Id, ctx);
        if (effectiveness == 0f) return 0;
        damage = (int)(damage * effectiveness);

        // Critical hit ×2
        if (isCritical)
            damage *= 2;

        return Math.Max(1, damage);
    }

    // Physical moves use Attack/Defense; Special moves use SpAtk/SpDef.
    private static bool IsPhysical(MoveData move) =>
        move.TypeId is "NORMAL" or "FIGHTING" or "FLYING" or "POISON" or "GROUND"
            or "ROCK" or "BUG" or "GHOST";

    private static int GetModifiedAtk(IBattleContext ctx, BattlePokemon attacker, MoveData move)
    {
        int stage = IsPhysical(move) ? ctx.AttackerStages.Attack : ctx.AttackerStages.SpAtk;
        int raw   = IsPhysical(move) ? attacker.Attack : attacker.SpAtk;
        int val   = StatCalculator.ApplyStage(raw, stage);
        // Burn halves physical attack dynamically — stat record is not mutated.
        // Source: engine/battle/effect_commands.asm CalculateDamage
        if (IsPhysical(move) && attacker.Status == PrimaryStatus.Burned)
            val = Math.Max(1, val / 2);
        return val;
    }

    private static int GetModifiedDef(IBattleContext ctx, BattlePokemon defender, MoveData move)
    {
        int stage = IsPhysical(move) ? ctx.DefenderStages.Defense : ctx.DefenderStages.SpDef;
        int raw = IsPhysical(move) ? defender.Defense : defender.SpDef;
        return StatCalculator.ApplyStage(raw, stage);
    }

    // On crit, use unmodified stats (ignore stage boosts/drops)
    private static int GetBaseStatForCrit(IBattleContext ctx, BattlePokemon attacker, MoveData move)
        => IsPhysical(move) ? attacker.Attack : attacker.SpAtk;

    private static int GetBaseDefForCrit(IBattleContext ctx, BattlePokemon defender, MoveData move)
        => IsPhysical(move) ? defender.Defense : defender.SpDef;
}
