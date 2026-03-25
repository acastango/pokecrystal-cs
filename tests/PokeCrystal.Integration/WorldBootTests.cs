namespace PokeCrystal.Integration;

using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.Data;
using PokeCrystal.Engine;
using PokeCrystal.Integration.Helpers;
using PokeCrystal.Schema;
using PokeCrystal.Scripting;
using PokeCrystal.World;
using Xunit;

/// <summary>
/// Smoke-tests that the full L0–L4 DI stack assembles and runs without error.
/// No MonoGame or Avalonia — purely logical layers.
/// </summary>
public sealed class WorldBootTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // L1 — Data
        var registry = (DataRegistry)DataLoader.LoadAll(DataPaths.DataBase);
        services.AddSingleton<IDataRegistry>(registry);

        // L2 — Engine (calculators)
        services.AddCrystalEngine();

        // L3 — Scripting
        services.AddCrystalScripting();

        // L4 — World
        services.AddCrystalWorld();

        return services.BuildServiceProvider();
    }

    // -----------------------------------------------------------------------
    // Service resolution
    // -----------------------------------------------------------------------

    [Fact]
    public void All_engine_calculators_resolve()
    {
        using var sp = BuildServices();

        Assert.NotNull(sp.GetRequiredService<IStatCalculator>());
        Assert.NotNull(sp.GetRequiredService<IDamageCalculator>());
        Assert.NotNull(sp.GetRequiredService<ITypeEffectivenessResolver>());
        Assert.NotNull(sp.GetRequiredService<ICatchCalculator>());
        Assert.NotNull(sp.GetRequiredService<IExperienceCalculator>());
    }

    [Fact]
    public void ScriptEngine_resolves()
    {
        using var sp = BuildServices();
        Assert.NotNull(sp.GetRequiredService<ScriptEngine>());
    }

    [Fact]
    public void OverworldEngine_resolves()
    {
        using var sp = BuildServices();
        Assert.NotNull(sp.GetRequiredService<OverworldEngine>());
    }

    // -----------------------------------------------------------------------
    // State machine: Start → Enter → Handle
    // Ticking 3 times on an empty map with no entry script.
    // -----------------------------------------------------------------------

    [Fact]
    public void Overworld_ticks_Start_Enter_Handle()
    {
        using var sp = BuildServices();
        var engine   = sp.GetRequiredService<OverworldEngine>();
        var maps     = sp.GetRequiredService<MapRegistry>();

        var ctx = new WorldContext
        {
            Maps          = maps,
            CurrentMapId  = "",     // no map loaded; TryGet returns false → no entry script
            MapStatus     = MapStatus.Start,
        };

        // Tick 1: Start → Enter (no entry script on empty map)
        engine.Tick(ctx);
        Assert.Equal(MapStatus.Enter, ctx.MapStatus);

        // Tick 2: Enter → Handle
        engine.Tick(ctx);
        Assert.Equal(MapStatus.Handle, ctx.MapStatus);
        Assert.True(ctx.EventsEnabled);

        // Tick 3: Handle — systems run (all no-ops with no map data), state stays Handle
        engine.Tick(ctx);
        Assert.Equal(MapStatus.Handle, ctx.MapStatus);
    }

    // -----------------------------------------------------------------------
    // Data integrity — registry loaded into the DI container has correct counts.
    // -----------------------------------------------------------------------

    [Fact]
    public void Registry_contains_all_251_species()
    {
        using var sp = BuildServices();
        var reg = sp.GetRequiredService<IDataRegistry>();
        Assert.Equal(251, reg.GetAll<SpeciesData>().Count);
    }
}
