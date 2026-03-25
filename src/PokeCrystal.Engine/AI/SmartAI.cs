namespace PokeCrystal.Engine.AI;

using PokeCrystal.Data;
using PokeCrystal.Schema;

/// <summary>
/// Gen 2 smart AI tier — scores moves by expected damage and type effectiveness.
/// Corresponds to AI_SMART flag. Prefers super-effective moves; avoids immune matchups.
/// </summary>
public sealed class SmartAI : IAIStrategy
{
    public string StrategyKey => "smart";

    private readonly ITypeEffectivenessResolver _typeResolver;
    private readonly Random _rng;

    public SmartAI(ITypeEffectivenessResolver typeResolver, Random? rng = null)
    {
        _typeResolver = typeResolver;
        _rng = rng ?? Random.Shared;
    }

    public int SelectMove(IBattleContext ctx, BattlePokemon ai, BattlePokemon opponent,
        MoveData[] moves)
    {
        int best = -1;
        float bestScore = float.MinValue;

        for (int i = 0; i < Math.Min(moves.Length, ai.PP.Length); i++)
        {
            if (ai.PP[i] == 0 || moves[i].Id == "NO_MOVE") continue;

            float score = ScoreMove(ctx, ai, opponent, moves[i]);
            if (score > bestScore || (score == bestScore && _rng.Next(2) == 0))
            {
                bestScore = score;
                best = i;
            }
        }

        return best >= 0 ? best : 0;
    }

    private float ScoreMove(IBattleContext ctx, BattlePokemon ai, BattlePokemon opponent,
        MoveData move)
    {
        if (move.Power == 0) return 1f; // Status move — neutral score

        float effectiveness = _typeResolver.GetMultiplier(
            move.TypeId, opponent.Type1Id, opponent.Type2Id, ctx);

        if (effectiveness == 0f) return -10f; // Never use immune moves

        float score = move.Power * effectiveness;

        // STAB bonus
        if (ai.Type1Id == move.TypeId || ai.Type2Id == move.TypeId)
            score *= 1.5f;

        return score;
    }
}
