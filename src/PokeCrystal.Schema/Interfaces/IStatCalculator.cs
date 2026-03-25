namespace PokeCrystal.Schema;

/// <summary>
/// Calculates battle-ready stats from species data, DVs, StatExp, and level.
/// L2 provides the Gen 2 formula. HP uses a separate formula.
/// </summary>
public interface IStatCalculator
{
    int CalcStat(SpeciesData species, DVs dvs, StatExp statExp, int level, StatType stat);
    int CalcHp(SpeciesData species, DVs dvs, StatExp statExp, int level);
}
