namespace PokeCrystal.Integration;

using PokeCrystal.Data;
using PokeCrystal.Integration.Helpers;
using PokeCrystal.Schema;
using Xunit;

/// <summary>
/// Verifies that DataLoader.LoadAll() correctly reads the full data/base/ tree.
/// Expected counts and spot-check values are derived from the pokecrystal-master ASM source.
/// </summary>
public sealed class DataLoaderTests
{
    // Loaded once for all tests in this class via a shared fixture.
    private static readonly IDataRegistry Registry =
        DataLoader.LoadAll(DataPaths.DataBase);

    // -----------------------------------------------------------------------
    // Record counts — these must match what extract_all.py produces.
    // -----------------------------------------------------------------------

    [Fact]
    public void Species_count_is_251()
    {
        var all = Registry.GetAll<SpeciesData>();
        Assert.Equal(251, all.Count);
    }

    [Fact]
    public void Moves_count_is_251()
    {
        var all = Registry.GetAll<MoveData>();
        Assert.Equal(251, all.Count);
    }

    [Fact]
    public void Items_count_is_227()
    {
        var all = Registry.GetAll<ItemData>();
        Assert.Equal(227, all.Count);
    }

    [Fact]
    public void Trainers_count_is_541()
    {
        var all = Registry.GetAll<TrainerData>();
        Assert.Equal(541, all.Count);
    }

    [Fact]
    public void TypeMatchups_at_least_111()
    {
        // Base: 103 table entries + 8 hardcoded immunities = 111.
        var all = Registry.GetAll<TypeMatchup>();
        Assert.True(all.Count >= 111, $"Expected ≥111 type matchups, got {all.Count}");
    }

    // -----------------------------------------------------------------------
    // Species spot-checks — values come from data/pokemon/base_stats/bulbasaur.asm
    // -----------------------------------------------------------------------

    [Fact]
    public void Bulbasaur_base_stats_match_ASM()
    {
        var b = Registry.Get<SpeciesData>("BULBASAUR");
        Assert.Equal(45, b.BaseHp);
        Assert.Equal(49, b.BaseAttack);
        Assert.Equal(49, b.BaseDefense);
        Assert.Equal(45, b.BaseSpeed);
        Assert.Equal(65, b.BaseSpAtk);
        Assert.Equal(65, b.BaseSpDef);
    }

    [Fact]
    public void Bulbasaur_type_is_GRASS_POISON()
    {
        var b = Registry.Get<SpeciesData>("BULBASAUR");
        Assert.Equal("GRASS",  b.Type1Id);
        Assert.Equal("POISON", b.Type2Id);
    }

    [Fact]
    public void Bulbasaur_catch_rate_is_45()
        => Assert.Equal(45, Registry.Get<SpeciesData>("BULBASAUR").CatchRate);

    [Fact]
    public void Bulbasaur_learnset_starts_with_Tackle_at_1()
    {
        var learnset = Registry.Get<SpeciesData>("BULBASAUR").Learnset;
        Assert.Contains(learnset, e => e.Level == 1 && e.MoveId == "TACKLE");
    }

    [Fact]
    public void Bulbasaur_evolves_to_Ivysaur_at_16()
    {
        var evos = Registry.Get<SpeciesData>("BULBASAUR").Evolutions;
        Assert.Single(evos);
        Assert.Equal(EvolutionMethod.Level, evos[0].Method);
        Assert.Equal("IVYSAUR", evos[0].TargetSpeciesId);
        Assert.Equal("16",      evos[0].Param);
    }

    [Fact]
    public void Mewtwo_has_no_evolutions()
        => Assert.Empty(Registry.Get<SpeciesData>("MEWTWO").Evolutions);

    // -----------------------------------------------------------------------
    // Move spot-checks — values from data/moves/moves.asm
    // -----------------------------------------------------------------------

    [Fact]
    public void Pound_stats_match_ASM()
    {
        var m = Registry.Get<MoveData>("POUND");
        Assert.Equal(40,       m.Power);
        Assert.Equal("NORMAL", m.TypeId);
        Assert.Equal(100,      m.Accuracy);
        Assert.Equal(35,       m.PP);
    }

    [Fact]
    public void Fire_Punch_type_is_FIRE()
        => Assert.Equal("FIRE", Registry.Get<MoveData>("FIRE_PUNCH").TypeId);

    // -----------------------------------------------------------------------
    // Item spot-checks — values from data/items/attributes.asm
    // -----------------------------------------------------------------------

    [Fact]
    public void Master_Ball_is_in_Balls_pocket()
        => Assert.Equal("Balls", Registry.Get<ItemData>("MASTER_BALL").Pocket.ToString());

    [Fact]
    public void Bicycle_is_a_KeyItem()
        => Assert.Equal("KeyItems", Registry.Get<ItemData>("BICYCLE").Pocket.ToString());
}
