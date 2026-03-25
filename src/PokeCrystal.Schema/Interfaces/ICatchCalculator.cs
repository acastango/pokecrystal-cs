namespace PokeCrystal.Schema;

/// <summary>
/// Determines whether a thrown Poké Ball catches the target.
/// L2 provides the Gen 2 formula (catch rate × ball modifier × HP factor).
/// </summary>
public interface ICatchCalculator
{
    /// <summary>
    /// Returns true if the catch succeeds.
    /// shakesCount (1-3) is set to the number of shake animations before break-free.
    /// </summary>
    bool TryCatch(IBattleContext ctx, BattlePokemon target, ItemData ball,
                  out int shakesCount);
}
