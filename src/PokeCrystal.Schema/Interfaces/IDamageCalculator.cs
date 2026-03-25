namespace PokeCrystal.Schema;

/// <summary>
/// Calculates raw damage for a move use. L2 provides the Gen 2 default.
/// Mods (L7) can replace this to implement custom damage formulas.
/// </summary>
public interface IDamageCalculator
{
    int Calculate(IBattleContext ctx, BattlePokemon attacker, BattlePokemon defender,
                  MoveData move, bool isCritical);
}
