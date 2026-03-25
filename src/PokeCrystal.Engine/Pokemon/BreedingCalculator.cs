namespace PokeCrystal.Engine.Pokemon;

using PokeCrystal.Data;
using PokeCrystal.Schema;

/// <summary>
/// Gen 2 breeding compatibility and egg generation.
/// Source: engine/pokemon/breeding.asm CheckBreedmonCompatibility
///
/// Rules:
///   1. Ditto + Ditto = incompatible.
///   2. Non-Ditto: must have compatible egg groups AND opposite genders (or one is Ditto).
///   3. Same Defense DV AND same lower-3-bits of Special DV → anti-clone block.
///   4. Same TrainerID → compatibility reduced (handled externally; not an outright block).
/// </summary>
public sealed class BreedingCalculator : IBreedingCalculator
{
    private const string DittoId = "DITTO";
    private const string EggGroupNone = "NONE";

    private readonly IDataRegistry _registry;
    private readonly Random _rng;

    public BreedingCalculator(IDataRegistry registry, Random? rng = null)
    {
        _registry = registry;
        _rng = rng ?? Random.Shared;
    }

    public bool AreCompatible(StoredPokemon p1, StoredPokemon p2,
        SpeciesData s1, SpeciesData s2)
    {
        bool d1 = s1.Id == DittoId;
        bool d2 = s2.Id == DittoId;

        // Ditto + Ditto is incompatible
        if (d1 && d2) return false;

        if (!d1 && !d2)
        {
            // Must share at least one egg group
            bool sharedGroup = s1.EggGroups.Any(g =>
                g != EggGroupNone && s2.EggGroups.Contains(g));
            if (!sharedGroup) return false;

            // Must be opposite genders (Undetermined group exempt)
            var gender1 = GetGender(p1, s1);
            var gender2 = GetGender(p2, s2);
            if (gender1 == Gender.Genderless || gender2 == Gender.Genderless) return false;
            if (gender1 == gender2) return false;
        }

        // Anti-clone check: same Defense DV AND same lower-3-bits of Special DV
        if (p1.DVs.Defense == p2.DVs.Defense &&
            (p1.DVs.Special & 0x07) == (p2.DVs.Special & 0x07))
            return false;

        return true;
    }

    public StoredPokemon GenerateEgg(StoredPokemon parent1, StoredPokemon parent2,
        SpeciesData species1, SpeciesData species2)
    {
        // Mother is the female (or Ditto if one parent is Ditto)
        StoredPokemon mother = species2.Id == DittoId ? parent1 : parent2;
        SpeciesData motherSpecies = species2.Id == DittoId ? species1 : species2;

        // Baby species = lowest evolution of mother's line (simplified: use mother's species)
        string babySpeciesId = motherSpecies.Id;

        // DVs: each bit is independently random (in Gen 2, DVs are fully random for eggs)
        var dvs = new DVs(
            Attack:  (byte)_rng.Next(16),
            Defense: (byte)_rng.Next(16),
            Speed:   (byte)_rng.Next(16),
            Special: (byte)_rng.Next(16)
        );

        // Egg moves from species egg move pool (mother passes egg moves down)
        var eggMoves = motherSpecies.EggMoves
            .Take(4)
            .ToArray();

        // Level-1 learnset
        var level1Moves = motherSpecies.Learnset
            .Where(l => l.Level == 1)
            .Select(l => l.MoveId)
            .ToArray();

        var moves = eggMoves.Concat(level1Moves)
            .Distinct()
            .Take(4)
            .ToArray();

        // Pad to 4 moves
        moves = moves.Concat(Enumerable.Repeat("NO_MOVE", 4 - moves.Length)).Take(4).ToArray();

        return new StoredPokemon(
            SpeciesId:       babySpeciesId,
            HeldItemId:      "NO_ITEM",
            Moves:           moves,
            TrainerId:       mother.TrainerId,
            Exp:             0,
            StatExp:         new StatExp(0, 0, 0, 0, 0),
            DVs:             dvs,
            PP:              moves.Select(_ => (byte)0).ToArray(),
            Happiness:       120,
            PokerusStatus:   0,
            CaughtTimeOfDay: TimeOfDay.Morning,
            CaughtLevel:     5,
            CaughtGender:    Gender.Male,
            CaughtLocationId: "EGG",
            Level:           5
        );
    }

    private static Gender GetGender(StoredPokemon mon, SpeciesData species)
    {
        if (species.GenderRatio < 0) return Gender.Genderless;
        if (species.GenderRatio == 0f) return Gender.Male;
        if (species.GenderRatio == 1f) return Gender.Female;
        // DV-based gender: Attack DV < threshold → Female
        int threshold = (int)(species.GenderRatio * 16);
        return mon.DVs.Attack < threshold ? Gender.Female : Gender.Male;
    }
}
