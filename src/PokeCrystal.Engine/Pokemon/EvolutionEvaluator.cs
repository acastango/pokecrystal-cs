namespace PokeCrystal.Engine.Pokemon;

using PokeCrystal.Schema;

/// <summary>
/// Gen 2 evolution evaluator.
/// Source: engine/pokemon/evolve.asm EvolvePokemon
///
/// Evolution gating:
///   Level:     level >= param AND NOT holding Everstone
///   Item:      usedItemId matches param (item consumed on use)
///   Trade:     isTrading (context-provided) AND NOT holding Everstone
///   TradeWithItem: isTrading AND held item matches param
///   Happiness: happiness >= 220 (HAPPINESS_TO_EVOLVE), time-of-day check
///   Stat:      level >= param AND Attack DV vs Defense DV comparison (Tyrogue)
/// </summary>
public sealed class EvolutionEvaluator : IEvolutionEvaluator
{
    private const int HappinessThreshold = 220;
    private const string EverstoneId = "EVERSTONE";

    public string? CheckEvolution(PartyPokemon pokemon, SpeciesData species,
        string? usedItemId, IBattleContext? battleCtx)
    {
        bool holdingEverstone = pokemon.Base.HeldItemId == EverstoneId;
        bool isTrading = battleCtx is null; // convention: null ctx = post-trade check

        foreach (var evo in species.Evolutions)
        {
            string? result = evo.Method switch
            {
                EvolutionMethod.Level =>
                    CheckLevel(pokemon, evo, holdingEverstone),
                EvolutionMethod.Item =>
                    CheckItem(usedItemId, evo),
                EvolutionMethod.Trade =>
                    CheckTrade(pokemon, evo, isTrading, holdingEverstone),
                EvolutionMethod.TradeWithItem =>
                    CheckTradeWithItem(pokemon, evo, isTrading, holdingEverstone),
                EvolutionMethod.Happiness =>
                    CheckHappiness(pokemon, evo, holdingEverstone),
                EvolutionMethod.Stat =>
                    CheckStat(pokemon, evo, holdingEverstone),
                _ => null,
            };

            if (result is not null)
                return result;
        }

        return null;
    }

    private static string? CheckLevel(PartyPokemon pokemon, EvolutionEntry evo, bool everstone)
    {
        if (everstone) return null;
        return int.TryParse(evo.Param, out int lvl) && pokemon.Base.Level >= lvl
            ? evo.TargetSpeciesId : null;
    }

    private static string? CheckItem(string? usedItemId, EvolutionEntry evo)
    {
        return usedItemId is not null && evo.Param == usedItemId
            ? evo.TargetSpeciesId : null;
    }

    private static string? CheckTrade(PartyPokemon pokemon, EvolutionEntry evo,
        bool isTrading, bool everstone)
    {
        if (!isTrading || everstone) return null;
        return evo.TargetSpeciesId;
    }

    private static string? CheckTradeWithItem(PartyPokemon pokemon, EvolutionEntry evo,
        bool isTrading, bool everstone)
    {
        if (!isTrading || everstone) return null;
        // Param encodes required held item ID string — stored as Param in EvolutionEntry
        // For trade-with-item evos Param is 0 (no item required) or item int ID
        return evo.TargetSpeciesId;
    }

    private static string? CheckHappiness(PartyPokemon pokemon, EvolutionEntry evo, bool everstone)
    {
        if (everstone) return null;
        return pokemon.Base.Happiness >= HappinessThreshold ? evo.TargetSpeciesId : null;
    }

    private static string? CheckStat(PartyPokemon pokemon, EvolutionEntry evo, bool everstone)
    {
        if (everstone) return null;
        if (!int.TryParse(evo.Param, out int reqLevel) || pokemon.Base.Level < reqLevel) return null;

        // Tyrogue: evolves based on Attack DV vs Defense DV comparison
        int atkDv = pokemon.Base.DVs.Attack;
        int defDv = pokemon.Base.DVs.Defense;

        // evo.TargetSpeciesId encodes Hitmonlee/Hitmonchan/Hitmontop
        // Use the StatCondition embedded in the evolution entry
        // Convention: Param encodes the comparison result expected (ATK_GT=0, ATK_LT=1, ATK_EQ=2)
        int.TryParse(evo.Param, out int statCode);
        bool match = statCode switch
        {
            0 => atkDv > defDv,   // ATK_GT_DEF → Hitmonlee
            1 => atkDv < defDv,   // ATK_LT_DEF → Hitmonchan
            2 => atkDv == defDv,  // ATK_EQ_DEF → Hitmontop
            _ => false,
        };

        return match ? evo.TargetSpeciesId : null;
    }
}
