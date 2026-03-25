namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Schema;

/// <summary>
/// Gen 2 stat calculator.
/// Formula (non-HP): ((base + dv) * 2 + sqrt(statExp) / 4) * level / 100 + 5
/// Formula (HP):     ((base + dv) * 2 + sqrt(statExp) / 4) * level / 100 + level + 10
/// Source: engine/pokemon/move_mon.asm CalcMonStatC
/// </summary>
public sealed class StatCalculator : IStatCalculator
{
    // data/battle/stat_multipliers.asm — index = stage + 6 (stage -6..+6)
    private static readonly (int Num, int Den)[] StatStageMultipliers =
    [
        (25, 100), // -6
        (28, 100), // -5
        (33, 100), // -4
        (40, 100), // -3
        (50, 100), // -2
        (66, 100), // -1
        ( 1,   1), //  0
        (15,  10), // +1
        ( 2,   1), // +2
        (25,  10), // +3
        ( 3,   1), // +4
        (35,  10), // +5
        ( 4,   1), // +6
    ];

    // data/battle/accuracy_multipliers.asm — index = stage + 6
    internal static readonly (int Num, int Den)[] AccuracyStageMultipliers =
    [
        ( 33, 100), // -6
        ( 36, 100), // -5
        ( 43, 100), // -4
        ( 50, 100), // -3
        ( 60, 100), // -2
        ( 75, 100), // -1
        (  1,   1), //  0
        (133, 100), // +1
        (166, 100), // +2
        (  2,   1), // +3
        (233, 100), // +4
        (133,  50), // +5
        (  3,   1), // +6
    ];

    public int CalcStat(SpeciesData species, DVs dvs, StatExp statExp, int level, StatType stat)
    {
        int baseStat = stat switch
        {
            StatType.Attack  => species.BaseAttack,
            StatType.Defense => species.BaseDefense,
            StatType.Speed   => species.BaseSpeed,
            StatType.SpAtk   => species.BaseSpAtk,
            StatType.SpDef   => species.BaseSpDef,
            _                => species.BaseHp,
        };
        byte dv = stat switch
        {
            StatType.Attack  => dvs.Attack,
            StatType.Defense => dvs.Defense,
            StatType.Speed   => dvs.Speed,
            // SpAtk and SpDef share the Special DV
            StatType.SpAtk   => dvs.Special,
            StatType.SpDef   => dvs.Special,
            _                => dvs.HpDv,
        };
        int statExpVal = stat switch
        {
            StatType.Attack  => statExp.Attack,
            StatType.Defense => statExp.Defense,
            StatType.Speed   => statExp.Speed,
            // SpAtk and SpDef share the Special StatExp
            StatType.SpAtk   => statExp.Special,
            StatType.SpDef   => statExp.Special,
            _                => statExp.Hp,
        };

        return CalcHpOrStat(baseStat, dv, statExpVal, level, stat == StatType.Hp);
    }

    public int CalcHp(SpeciesData species, DVs dvs, StatExp statExp, int level)
        => CalcHpOrStat(species.BaseHp, dvs.HpDv, statExp.Hp, level, isHp: true);

    /// <summary>Applies a stat stage multiplier. Stage is 7-based (neutral=7).</summary>
    public static int ApplyStage(int rawStat, int stage7)
    {
        int idx = Math.Clamp(stage7 - 1, 0, 12); // stage7: 1=-6, 7=0, 13=+6
        var (num, den) = StatStageMultipliers[idx];
        return Math.Clamp(rawStat * num / den, 1, 999);
    }

    /// <summary>Applies an accuracy/evasion stage. Stage is 7-based (neutral=7).</summary>
    public static int ApplyAccuracyStage(int baseAccuracy, int stage7)
    {
        int idx = Math.Clamp(stage7 - 1, 0, 12);
        var (num, den) = AccuracyStageMultipliers[idx];
        return baseAccuracy * num / den;
    }

    private static int CalcHpOrStat(int baseStat, byte dv, int statExpVal, int level, bool isHp)
    {
        int sqrt = IntSqrt(statExpVal);
        int val = ((baseStat + dv) * 2 + sqrt / 4) * level / 100;
        return isHp ? val + level + 10 : val + 5;
    }

    private static int IntSqrt(int n)
    {
        if (n <= 0) return 0;
        int x = (int)Math.Sqrt(n);
        while ((x + 1) * (x + 1) <= n) x++;
        while (x * x > n) x--;
        return x;
    }
}
