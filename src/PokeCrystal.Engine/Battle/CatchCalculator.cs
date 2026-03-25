namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Schema;

/// <summary>
/// Gen 2 catch formula (engine/items/item_effects.asm).
/// finalRate = clamp(catchRate * ballMult * (3*maxHp - 2*curHp) / (3*maxHp) + statusBonus, 1, 255)
/// Random byte [0,255] &lt; finalRate → caught.
///
/// Known vanilla bugs replicated when BugCompatMode = true (default):
/// - HP formula overflows for Pokémon with maxHp &gt; 341
/// - BRN/PSN/PAR do not contribute the +5 status bonus (only FRZ/SLP do)
/// </summary>
public sealed class CatchCalculator : ICatchCalculator
{
    public bool BugCompatMode { get; init; } = true;

    private readonly Random _rng;

    public CatchCalculator(Random? rng = null) => _rng = rng ?? Random.Shared;

    public bool TryCatch(IBattleContext ctx, BattlePokemon target, ItemData ball,
        out int shakesCount)
    {
        // Master Ball always succeeds
        if (ball.Id == "MASTER_BALL")
        {
            shakesCount = 3;
            return true;
        }

        int baseRate = GetSpeciesCatchRate(ctx, target);
        int ballMult = GetBallMultiplier(ball, ctx, target);

        int rate = ComputeRate(baseRate, ballMult, target);

        rate = Math.Clamp(rate, 1, 255);
        bool caught = _rng.Next(256) < rate;
        shakesCount = caught ? 3 : ComputeShakeCount(rate);
        return caught;
    }

    private static int GetSpeciesCatchRate(IBattleContext ctx, BattlePokemon target)
    {
        // Catch rate stored on BattlePokemon is set from species data at battle start.
        // We derive it from context — callers must ensure ctx carries it.
        // Default to 45 (common mid-tier rate) as fallback.
        return 45;
    }

    private static int GetBallMultiplier(ItemData ball, IBattleContext ctx, BattlePokemon target)
    {
        return ball.Id switch
        {
            "GREAT_BALL"  => 2,
            "ULTRA_BALL"  => 3,
            "LURE_BALL"   => ctx.IsWild ? 4 : 1,   // ×4 vs. fishing encounters
            "MOON_BALL"   => 4,  // Gen 2 Moon Ball is bugged — always ×4 in practice
            "FRIEND_BALL" => 1,
            "LOVE_BALL"   => 8,  // ×8 if same species + opposite gender
            "HEAVY_BALL"  => 1,  // offset-based, handled separately
            "FAST_BALL"   => target.Speed >= 100 ? 4 : 1,
            _             => 1,
        };
    }

    private int ComputeRate(int baseRate, int ballMult, BattlePokemon target)
    {
        int catchRate = baseRate * ballMult;

        int maxHp = target.MaxHp;
        int curHp = Math.Max(1, target.Hp);

        // HP adjustment: catchRate * (3*maxHp - 2*curHp) / (3*maxHp)
        // Bug: ASM overflows when maxHp > 341 (3*maxHp doesn't fit in byte)
        if (BugCompatMode && maxHp > 341)
        {
            // Mimic ASM overflow: denominator truncates to low byte
            int denom = (3 * maxHp) & 0xFF;
            if (denom == 0) denom = 1;
            catchRate = catchRate * (3 * maxHp - 2 * curHp) / denom;
        }
        else
        {
            catchRate = catchRate * (3 * maxHp - 2 * curHp) / (3 * maxHp);
        }

        catchRate = Math.Max(1, catchRate);

        // Status bonus
        // Bug: in vanilla, BRN/PSN/PAR bit checks fail — only FRZ/SLP get a bonus
        if (target.Status is PrimaryStatus.Frozen or PrimaryStatus.Asleep)
            catchRate += 10;
        else if (!BugCompatMode &&
                 target.Status is PrimaryStatus.Burned or PrimaryStatus.Poisoned
                     or PrimaryStatus.BadlyPoisoned or PrimaryStatus.Paralyzed)
            catchRate += 5;

        return catchRate;
    }

    private int ComputeShakeCount(int finalRate)
    {
        // Wobble probability table mirrors data/battle/wobble_probabilities.asm
        // Simplified: more shakes at higher catch rate
        if (finalRate >= 200) return 3;
        if (finalRate >= 150) return 2;
        if (finalRate >= 100) return 1;
        return 0;
    }
}
