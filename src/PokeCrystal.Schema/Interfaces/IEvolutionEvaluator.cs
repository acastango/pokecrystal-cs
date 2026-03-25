namespace PokeCrystal.Schema;

/// <summary>
/// Checks whether a Pokémon is ready to evolve after a battle or item use.
/// L2 provides the Gen 2 condition checks per EvolutionMethod.
/// Mods can add new evolution methods by registering new evaluators.
/// </summary>
public interface IEvolutionEvaluator
{
    /// <summary>Returns the target species ID if evolution should trigger, null otherwise.</summary>
    string? CheckEvolution(PartyPokemon pokemon, SpeciesData species,
                           string? usedItemId, IBattleContext? battleCtx);
}
