namespace PokeCrystal.Integration;

using PokeCrystal.Data;
using PokeCrystal.Engine.Battle;
using PokeCrystal.Integration.Helpers;
using PokeCrystal.Schema;
using Xunit;

/// <summary>
/// Type effectiveness spot-checks. Expected values are from:
///   data/types/type_matchups.asm   — explicit entries (0.5x, 2.0x)
///   extract_types.py               — hardcoded immunities (0.0x)
///   Default behaviour              — unlisted pairs = 1.0x
///
/// Tests use real data/base/type_matchups.json loaded via DataLoader.
/// </summary>
public sealed class TypeEffectivenessTests
{
    private static readonly TypeEffectivenessResolver Resolver;

    static TypeEffectivenessTests()
    {
        var registry = (DataRegistry)DataLoader.LoadAll(DataPaths.DataBase);
        Resolver = new TypeEffectivenessResolver(registry);
    }

    private static float Mult(string atk, string def1, string def2 = "")
    {
        if (string.IsNullOrEmpty(def2)) def2 = def1;  // mono-type: pass same type twice
        return Resolver.GetMultiplier(atk, def1, def2, StubBattleContext.Default);
    }

    // -----------------------------------------------------------------------
    // Super effective (2.0×) — from data/types/type_matchups.asm
    // -----------------------------------------------------------------------

    [Fact] public void Fire_vs_Grass_is_2x()       => Assert.Equal(2.0f, Mult("FIRE",     "GRASS"));
    [Fact] public void Water_vs_Fire_is_2x()        => Assert.Equal(2.0f, Mult("WATER",    "FIRE"));
    [Fact] public void Electric_vs_Water_is_2x()   => Assert.Equal(2.0f, Mult("ELECTRIC", "WATER"));
    [Fact] public void Ice_vs_Dragon_is_2x()        => Assert.Equal(2.0f, Mult("ICE",      "DRAGON"));
    [Fact] public void Fighting_vs_Normal_is_2x()  => Assert.Equal(2.0f, Mult("FIGHTING", "NORMAL"));

    // -----------------------------------------------------------------------
    // Not very effective (0.5×) — from data/types/type_matchups.asm
    // -----------------------------------------------------------------------

    [Fact] public void Normal_vs_Rock_is_half()    => Assert.Equal(0.5f, Mult("NORMAL",  "ROCK"));
    [Fact] public void Water_vs_Water_is_half()    => Assert.Equal(0.5f, Mult("WATER",   "WATER"));
    [Fact] public void Fire_vs_Dragon_is_half()    => Assert.Equal(0.5f, Mult("FIRE",    "DRAGON"));

    // -----------------------------------------------------------------------
    // Immunities (0.0×) — hardcoded by extract_types.py from engine/battle
    // -----------------------------------------------------------------------

    [Fact] public void Normal_vs_Ghost_is_immune()    => Assert.Equal(0.0f, Mult("NORMAL",   "GHOST"));
    [Fact] public void Electric_vs_Ground_is_immune() => Assert.Equal(0.0f, Mult("ELECTRIC", "GROUND"));
    [Fact] public void Ground_vs_Flying_is_immune()   => Assert.Equal(0.0f, Mult("GROUND",   "FLYING"));
    [Fact] public void Poison_vs_Steel_is_immune()    => Assert.Equal(0.0f, Mult("POISON",   "STEEL"));

    // -----------------------------------------------------------------------
    // Normal effectiveness (1.0×) — unlisted pairs default to 1×
    // -----------------------------------------------------------------------

    [Fact] public void Normal_vs_Normal_is_1x()    => Assert.Equal(1.0f, Mult("NORMAL",  "NORMAL"));
    [Fact] public void Water_vs_Grass_is_half()    => Assert.Equal(0.5f, Mult("WATER",   "GRASS"));

    // -----------------------------------------------------------------------
    // Dual-type interactions
    // FIRE attacking GRASS/POISON: 2.0 × 1.0 = 2.0
    // -----------------------------------------------------------------------

    [Fact]
    public void Fire_vs_Grass_Poison_dual_type_is_2x()
    {
        float mult = Resolver.GetMultiplier("FIRE", "GRASS", "POISON",
            StubBattleContext.Default);
        Assert.Equal(2.0f, mult);
    }

    // GROUND vs FLYING/ELECTRIC: 0.0 (ground immunity) — short-circuits at 0
    [Fact]
    public void Ground_vs_Flying_Electric_is_0x()
    {
        float mult = Resolver.GetMultiplier("GROUND", "FLYING", "ELECTRIC",
            StubBattleContext.Default);
        Assert.Equal(0.0f, mult);
    }
}
