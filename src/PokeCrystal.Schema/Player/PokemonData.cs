namespace PokeCrystal.Schema;

/// <summary>
/// Pokémon-related persistent data — mirrors wPokemonData WRAM block.
/// Pokédex seen/caught use EventFlagSet for string-keyed species lookups.
/// Roamers (Raikou, Entei, Suicune) are tracked here; their current map
/// updates as the player moves through routes.
/// </summary>
public record PokemonData(
    List<PartyPokemon> Party,              // max 6
    EventFlagSet PokedexCaught,
    EventFlagSet PokedexSeen,
    bool[] UnownDex,                       // 28 Unown forms
    DayCareSlot? DayCareSlot1,
    DayCareSlot? DayCareSlot2,
    int StepsToEgg,
    List<RoamingPokemon> Roamers           // Raikou, Entei, Suicune
);

public record DayCareSlot(StoredPokemon Pokemon, int StepsDeposited);

public record RoamingPokemon(
    string SpeciesId,
    byte Level,
    string CurrentMapId,
    int CurrentHp,
    DVs DVs
);
