namespace PokeCrystal.Schema;

/// <summary>
/// Determines breeding compatibility and egg generation.
/// L2 provides Gen 2 rules (egg groups, Ditto, gender).
/// </summary>
public interface IBreedingCalculator
{
    bool AreCompatible(StoredPokemon parent1, StoredPokemon parent2,
                       SpeciesData species1, SpeciesData species2);

    StoredPokemon GenerateEgg(StoredPokemon parent1, StoredPokemon parent2,
                              SpeciesData species1, SpeciesData species2);
}
