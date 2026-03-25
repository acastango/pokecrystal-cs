namespace PokeCrystal.Schema;

/// <summary>
/// Selects a move index (0-3) for an AI-controlled Pokémon.
/// L2 provides Gen 2 AI tiers (random, smart, etc.) keyed by string.
/// Mods can register custom strategies.
/// </summary>
public interface IAIStrategy
{
    string StrategyKey { get; }

    /// <summary>Returns the move slot index (0-3) to use this turn.</summary>
    int SelectMove(IBattleContext ctx, BattlePokemon ai, BattlePokemon opponent,
                   MoveData[] moves);
}
