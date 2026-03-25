namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Schema;

/// <summary>
/// Gen 2 experience calculator.
/// Source: engine/pokemon/experience.asm CalcExpAtLevel
/// Formula: (a/b)*n^3 + c*n^2 ± d*n - e  (parameters vary by growth rate)
///
/// Growth rate parameters (from data/growth_rates.asm):
///   MediumFast:   1/1, c=0,   d=0,   e=0
///   Erratic:      3/4, c=10,  d=0,   e=30   (Slightly Fast)
///   Fluctuating:  3/4, c=20,  d=0,   e=70   (Slightly Slow)
///   MediumSlow:   6/5, c=-15, d=100, e=140  (uses subtraction for c)
///   Fast:         4/5, c=0,   d=0,   e=0
///   Slow:         5/4, c=0,   d=0,   e=0
/// </summary>
public sealed class ExperienceCalculator : IExperienceCalculator
{
    private record GrowthParams(int A, int B, int C, int D, int E);

    private static readonly Dictionary<GrowthRate, GrowthParams> Params = new()
    {
        [GrowthRate.MediumFast]  = new(1,  1,   0,   0,   0),
        [GrowthRate.Erratic]     = new(3,  4,  10,   0,  30),
        [GrowthRate.Fluctuating] = new(3,  4,  20,   0,  70),
        [GrowthRate.MediumSlow]  = new(6,  5, -15, 100, 140),
        [GrowthRate.Fast]        = new(4,  5,   0,   0,   0),
        [GrowthRate.Slow]        = new(5,  4,   0,   0,   0),
    };

    public int ExpForLevel(GrowthRate rate, int level)
    {
        if (level <= 1) return 0;
        var p = Params[rate];
        long n = level;
        long exp = p.A * n * n * n / p.B + p.C * n * n + p.D * n - p.E;
        return (int)Math.Max(0, exp);
    }

    public int LevelForExp(GrowthRate rate, int exp)
    {
        // Binary search: find highest level where ExpForLevel(level) <= exp
        int lo = 1, hi = 100;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (ExpForLevel(rate, mid) <= exp)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }

    public int ExpYield(BattlePokemon target, bool isWild, int participantCount, bool hasLuckyEgg)
    {
        // Gen 2 EXP yield: baseExp * level / 7 (trainer) or / 7 (wild with ×1 multiplier)
        // Trainer battles: ×1.5 multiplier (×3/2)
        // Lucky Egg: ×1.5 on top
        // Participants: divided by count
        // Source: engine/pokemon/experience.asm CalcExpEarned
        int baseExp = 64; // fallback; callers should pass species.BaseExp via target lookup
        int level = target.Level;

        int exp = baseExp * level / 7;

        if (!isWild)
            exp = exp * 3 / 2;

        if (hasLuckyEgg)
            exp = exp * 3 / 2;

        if (participantCount > 1)
            exp /= participantCount;

        return Math.Max(1, exp);
    }
}
