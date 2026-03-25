namespace PokeCrystal.Schema;

/// <summary>
/// Applies a move's secondary or status effect (not raw damage).
/// One implementation per effect key; registered in L2.
/// Mods register new keys in L7.
/// </summary>
public interface IMoveEffect
{
    string EffectKey { get; }

    void Apply(IBattleContext ctx, BattlePokemon user, BattlePokemon target, MoveData move);
}
