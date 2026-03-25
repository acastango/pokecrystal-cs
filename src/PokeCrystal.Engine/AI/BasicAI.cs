namespace PokeCrystal.Engine.AI;

using PokeCrystal.Schema;

/// <summary>
/// Gen 2 basic AI tier — selects a random legal move (non-zero PP).
/// Corresponds to AI_BASIC flag in data/trainers/attributes.asm.
/// </summary>
public sealed class BasicAI : IAIStrategy
{
    public string StrategyKey => "basic";

    private readonly Random _rng;

    public BasicAI(Random? rng = null) => _rng = rng ?? Random.Shared;

    public int SelectMove(IBattleContext ctx, BattlePokemon ai, BattlePokemon opponent,
        MoveData[] moves)
    {
        // Collect slots with remaining PP
        var usable = Enumerable.Range(0, Math.Min(moves.Length, ai.PP.Length))
            .Where(i => ai.PP[i] > 0 && moves[i].Id != "NO_MOVE")
            .ToList();

        if (usable.Count == 0)
            return 0; // Struggle — engine handles this

        return usable[_rng.Next(usable.Count)];
    }
}
