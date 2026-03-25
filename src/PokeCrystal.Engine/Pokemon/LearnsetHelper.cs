namespace PokeCrystal.Engine.Pokemon;

using PokeCrystal.Schema;

/// <summary>
/// Utility helpers for learnset queries.
/// Source: engine/pokemon/learn.asm, engine/items/tmhm.asm
/// </summary>
public static class LearnsetHelper
{
    /// <summary>Returns all moves learned at exactly the given level.</summary>
    public static IEnumerable<string> GetMovesAtLevel(SpeciesData species, int level)
        => species.Learnset
            .Where(e => e.Level == level)
            .Select(e => e.MoveId);

    /// <summary>Returns all moves a species can learn from level 1 to the given level.</summary>
    public static IEnumerable<string> GetAllMovesUpToLevel(SpeciesData species, int level)
        => species.Learnset
            .Where(e => e.Level <= level)
            .Select(e => e.MoveId)
            .Distinct();

    /// <summary>Returns true if the species can learn the given move via TM or HM.</summary>
    public static bool CanLearnViaTmHm(SpeciesData species, string moveId)
        => species.TmHmMoves.Contains(moveId);
}
