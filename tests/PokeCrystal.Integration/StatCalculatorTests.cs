namespace PokeCrystal.Integration;

using PokeCrystal.Engine.Battle;
using PokeCrystal.Schema;
using Xunit;

/// <summary>
/// Canonical Gen 2 stat values derived from engine/pokemon/move_mon.asm CalcMonStatC.
/// Formula (non-HP): ((base + dv) * 2 + IntSqrt(statExp) / 4) * level / 100 + 5
/// Formula (HP):     ((base + dv) * 2 + IntSqrt(statExp) / 4) * level / 100 + level + 10
///
/// All expected values are computed by hand from the ASM formula.
/// </summary>
public sealed class StatCalculatorTests
{
    private static readonly StatCalculator Calc = new();

    // Bulbasaur base stats (from data/pokemon/base_stats/bulbasaur.asm):
    //   HP=45, Atk=49, Def=49, Spd=45, SpAtk=65, SpDef=65
    private static readonly SpeciesData Bulbasaur = new(
        Id: "BULBASAUR", Name: "BULBASAUR",
        BaseHp: 45, BaseAttack: 49, BaseDefense: 49,
        BaseSpeed: 45, BaseSpAtk: 65, BaseSpDef: 65,
        Type1Id: "GRASS", Type2Id: "POISON",
        GrowthRate: GrowthRate.MediumSlow,
        EggGroups: [], GenderRatio: 0.125f,
        CatchRate: 45, BaseExp: 64, HatchCycles: 20,
        TmHmMoves: [], Learnset: [], Evolutions: [], EggMoves: [],
        SpriteId: "bulbasaur", CryId: "bulbasaur");

    private static readonly DVs ZeroDvs   = new(0, 0, 0, 0);
    private static readonly DVs MaxDvs    = new(15, 15, 15, 15);
    private static readonly StatExp ZeroSE = new(0, 0, 0, 0, 0);
    private static readonly StatExp MaxSE  = new(65535, 65535, 65535, 65535, 65535);

    // -----------------------------------------------------------------------
    // L50, DV=0, StatExp=0  — "fresh catch" baseline
    // Computation: ((base+0)*2 + 0) * 50/100 + offset
    // -----------------------------------------------------------------------

    [Fact]
    public void Bulbasaur_HP_L50_zero_dvs_se()
    {
        // ((45+0)*2 + 0) * 50/100 + 50 + 10 = 45 + 60 = 105
        int hp = Calc.CalcHp(Bulbasaur, ZeroDvs, ZeroSE, 50);
        Assert.Equal(105, hp);
    }

    [Fact]
    public void Bulbasaur_Attack_L50_zero_dvs_se()
    {
        // ((49+0)*2 + 0) * 50/100 + 5 = 49 + 5 = 54
        int atk = Calc.CalcStat(Bulbasaur, ZeroDvs, ZeroSE, 50, StatType.Attack);
        Assert.Equal(54, atk);
    }

    [Fact]
    public void Bulbasaur_Defense_L50_zero_dvs_se()
    {
        // ((49+0)*2 + 0) * 50/100 + 5 = 54
        int def = Calc.CalcStat(Bulbasaur, ZeroDvs, ZeroSE, 50, StatType.Defense);
        Assert.Equal(54, def);
    }

    [Fact]
    public void Bulbasaur_Speed_L50_zero_dvs_se()
    {
        // ((45+0)*2 + 0) * 50/100 + 5 = 45 + 5 = 50
        int spd = Calc.CalcStat(Bulbasaur, ZeroDvs, ZeroSE, 50, StatType.Speed);
        Assert.Equal(50, spd);
    }

    [Fact]
    public void Bulbasaur_SpAtk_L50_zero_dvs_se()
    {
        // ((65+0)*2 + 0) * 50/100 + 5 = 65 + 5 = 70
        int spa = Calc.CalcStat(Bulbasaur, ZeroDvs, ZeroSE, 50, StatType.SpAtk);
        Assert.Equal(70, spa);
    }

    [Fact]
    public void Bulbasaur_SpDef_L50_zero_dvs_se()
    {
        // SpDef shares Special DV/SE with SpAtk in Gen 2 → same result
        int spd = Calc.CalcStat(Bulbasaur, ZeroDvs, ZeroSE, 50, StatType.SpDef);
        Assert.Equal(70, spd);
    }

    // -----------------------------------------------------------------------
    // L100, DV=15, StatExp=65535  — maximum possible values
    // IntSqrt(65535)=255; 255/4=63
    // -----------------------------------------------------------------------

    [Fact]
    public void Bulbasaur_HP_L100_max_dvs_se()
    {
        // ((45+15)*2 + 63) * 100/100 + 100 + 10 = 183 + 110 = 293
        int hp = Calc.CalcHp(Bulbasaur, MaxDvs, MaxSE, 100);
        Assert.Equal(293, hp);
    }

    [Fact]
    public void Bulbasaur_Attack_L100_max_dvs_se()
    {
        // ((49+15)*2 + 63) * 100/100 + 5 = 191 + 5 = 196
        int atk = Calc.CalcStat(Bulbasaur, MaxDvs, MaxSE, 100, StatType.Attack);
        Assert.Equal(196, atk);
    }

    // -----------------------------------------------------------------------
    // Stat stage multipliers — source: data/battle/stat_multipliers.asm
    // Stage is 7-based: neutral=7, min=1(-6), max=13(+6)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(7,  100)]  // neutral: 1/1
    [InlineData(1,   25)]  // -6: 25/100
    [InlineData(13, 400)]  // +6: 4/1
    [InlineData(8,  150)]  // +1: 15/10
    [InlineData(6,   66)]  // -1: 66/100
    public void ApplyStage_matches_ASM_table(int stage7, int expected)
    {
        int result = StatCalculator.ApplyStage(100, stage7);
        Assert.Equal(expected, result);
    }

    // -----------------------------------------------------------------------
    // CalcStat via IStatCalculator interface (ensure DI resolution path works)
    // -----------------------------------------------------------------------

    [Fact]
    public void IStatCalculator_HP_matches_CalcHp()
    {
        IStatCalculator iCalc = Calc;
        int viaStat = iCalc.CalcStat(Bulbasaur, ZeroDvs, ZeroSE, 50, StatType.Hp);
        int viaHp   = iCalc.CalcHp(Bulbasaur, ZeroDvs, ZeroSE, 50);
        Assert.Equal(viaHp, viaStat);
    }
}
