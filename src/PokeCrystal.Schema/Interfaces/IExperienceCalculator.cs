namespace PokeCrystal.Schema;

/// <summary>
/// Calculates EXP yield and level thresholds.
/// L2 provides Gen 2 formulas per growth rate.
/// </summary>
public interface IExperienceCalculator
{
    /// <summary>EXP gained by defeating target (before split / Exp. Share).</summary>
    int ExpYield(BattlePokemon target, bool isWild, int participantCount,
                 bool hasLuckyEgg);

    /// <summary>Minimum EXP required to reach the given level.</summary>
    int ExpForLevel(GrowthRate growthRate, int level);

    /// <summary>Level for the given total EXP amount.</summary>
    int LevelForExp(GrowthRate growthRate, int exp);
}
