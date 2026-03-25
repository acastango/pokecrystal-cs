namespace PokeCrystal.Integration;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.Data;
using PokeCrystal.Engine;
using PokeCrystal.Engine.Battle;
using PokeCrystal.Integration.Helpers;
using PokeCrystal.Schema;
using PokeCrystal.Scripting;
using PokeCrystal.World;
using Xunit;

/// <summary>
/// L16 integration tests — exercises the full play loop headlessly:
///   New game bootstrap → overworld tick → wild encounter trigger →
///   battle turn → PlayerWon → reload map.
///
/// No MonoGame, no rendering. Pure engine/world/data layers.
/// </summary>
public sealed class FullLoopTests
{
    // -------------------------------------------------------------------------
    // Shared DI setup (L1–L4, real data files)
    // -------------------------------------------------------------------------

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var registry = (DataRegistry)DataLoader.LoadAll(DataPaths.DataBase);
        services.AddSingleton<IDataRegistry>(registry);
        services.AddCrystalEngine();
        services.AddCrystalScripting();
        services.AddCrystalWorld();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Loads ROUTE_29 into the map registry and returns a minimal WorldContext
    /// placed at (5,5) on ROUTE_29, ready for MapStatus.Start.
    /// </summary>
    private static WorldContext BuildRouteCtx(IServiceProvider sp)
    {
        var maps   = sp.GetRequiredService<MapRegistry>();
        var loader = sp.GetRequiredService<MapLoader>();
        loader.LoadAll(Path.Combine(DataPaths.DataBase, "maps"));

        return new WorldContext
        {
            Maps            = maps,
            CurrentMapId    = "ROUTE_29",
            PlayerX         = 5,
            PlayerY         = 5,
            Facing          = FacingDirection.Down,
            MapStatus       = MapStatus.Start,
            CurrentTimeOfDay = TimeOfDay.Day,
        };
    }

    /// <summary>
    /// Builds a Cyndaquil L5 BattlePokemon using real IStatCalculator + IDataRegistry.
    /// </summary>
    private static BattlePokemon BuildCyndaquil(IServiceProvider sp)
    {
        var data    = sp.GetRequiredService<IDataRegistry>();
        var stats   = sp.GetRequiredService<IStatCalculator>();
        var species = data.Get<SpeciesData>("CYNDAQUIL");
        var dvs     = new DVs(10, 10, 10, 10);
        var statExp = new StatExp(0, 0, 0, 0, 0);
        const int level = 5;
        int maxHp = stats.CalcHp(species, dvs, statExp, level);
        return new BattlePokemon(
            SpeciesId:    "CYNDAQUIL",
            HeldItemId:   "NO_ITEM",
            Moves:        ["TACKLE", "LEER", "NO_MOVE", "NO_MOVE"],
            DVs:          dvs,
            PP:           [35, 30, 0, 0],
            Happiness:    70,
            Level:        level,
            Status:       PrimaryStatus.None,
            SleepCounter: 0,
            Hp:           maxHp,
            MaxHp:        maxHp,
            Attack:       stats.CalcStat(species, dvs, statExp, level, StatType.Attack),
            Defense:      stats.CalcStat(species, dvs, statExp, level, StatType.Defense),
            Speed:        stats.CalcStat(species, dvs, statExp, level, StatType.Speed),
            SpAtk:        stats.CalcStat(species, dvs, statExp, level, StatType.SpAtk),
            SpDef:        stats.CalcStat(species, dvs, statExp, level, StatType.SpDef),
            Type1Id:      species.Type1Id,
            Type2Id:      species.Type2Id);
    }

    // -------------------------------------------------------------------------
    // 1. Overworld ticks Start → Enter → Handle on a real ROUTE_29 map
    // -------------------------------------------------------------------------

    [Fact]
    public void NewGame_overworld_ticks_Start_Enter_Handle_on_ROUTE29()
    {
        using var sp = BuildServices();
        var engine = sp.GetRequiredService<OverworldEngine>();
        var ctx    = BuildRouteCtx(sp);

        // ROUTE_29 has no MapScriptId → no entry script fires
        engine.Tick(ctx);
        Assert.Equal(MapStatus.Enter, ctx.MapStatus);

        engine.Tick(ctx);
        Assert.Equal(MapStatus.Handle, ctx.MapStatus);
        Assert.True(ctx.EventsEnabled);

        engine.Tick(ctx); // systems update; WildEncounterSystem may or may not fire
        Assert.True(ctx.MapStatus is MapStatus.Handle or MapStatus.Done);
    }

    // -------------------------------------------------------------------------
    // 2. Wild encounter trigger: LoadWildMon + StartBattle sets PendingBattle
    // -------------------------------------------------------------------------

    [Fact]
    public void WildEncounter_trigger_populates_PendingBattle()
    {
        using var sp = BuildServices();
        var engine = sp.GetRequiredService<OverworldEngine>();
        var ctx    = BuildRouteCtx(sp);

        // Advance to Handle state
        engine.Tick(ctx); engine.Tick(ctx);
        Assert.Equal(MapStatus.Handle, ctx.MapStatus);

        // Simulate WildEncounterSystem selecting a PIDGEY slot
        ctx.LoadWildMon("PIDGEY", 3);
        ctx.StartBattle();

        Assert.NotNull(ctx.PendingBattle);
        Assert.True(ctx.PendingBattle!.IsWild);
        Assert.Equal("PIDGEY", ctx.PendingBattle.WildSpeciesId);
        Assert.Equal(3, ctx.PendingBattle.WildLevel);
        Assert.Equal(MapStatus.Done, ctx.MapStatus);
    }

    // -------------------------------------------------------------------------
    // 3. Battle turn: Cyndaquil KOs a 1-HP Pidgey → PlayerWon on first turn
    // -------------------------------------------------------------------------

    [Fact]
    public void Battle_first_turn_KOs_weak_opponent_gives_PlayerWon()
    {
        using var sp = BuildServices();
        var data   = sp.GetRequiredService<IDataRegistry>();
        var engine = sp.GetRequiredService<BattleEngine>();
        var player = BuildCyndaquil(sp);

        // Build a 1-HP wild Pidgey — guaranteed KO by any damage move
        var pidgeySpecies = data.Get<SpeciesData>("PIDGEY");
        var wildPidgey = new BattlePokemon(
            SpeciesId:    "PIDGEY",
            HeldItemId:   "NO_ITEM",
            Moves:        ["TACKLE", "NO_MOVE", "NO_MOVE", "NO_MOVE"],
            DVs:          new DVs(0, 0, 0, 0),
            PP:           [35, 0, 0, 0],
            Happiness:    70,
            Level:        2,
            Status:       PrimaryStatus.None,
            SleepCounter: 0,
            Hp:    1, MaxHp: 1,
            Attack: 5, Defense: 5, Speed: 5, SpAtk: 5, SpDef: 5,
            Type1Id: pidgeySpecies.Type1Id,
            Type2Id: pidgeySpecies.Type2Id);

        var state  = new BattleState(player, wildPidgey, isWild: true);
        var events = new List<BattleEvent>();

        var outcome = engine.ExecuteTurn(state, new UseMoveAction(0), events);

        Assert.Equal(BattleOutcome.PlayerWon, outcome);
        Assert.Contains(events, e => e is FaintedEvent { IsPlayer: false });
    }

    // -------------------------------------------------------------------------
    // 4. ReloadMapAfterBattle resets MapStatus to Start
    // -------------------------------------------------------------------------

    [Fact]
    public void ReloadMapAfterBattle_resets_MapStatus_to_Start()
    {
        var ctx = new WorldContext { MapStatus = MapStatus.Done };
        ctx.ReloadMapAfterBattle();
        Assert.Equal(MapStatus.Start, ctx.MapStatus);
    }

    // -------------------------------------------------------------------------
    // 5. Full loop: new game → inject encounter → battle → reload
    // -------------------------------------------------------------------------

    [Fact]
    public void Full_loop_NewGame_battle_PlayerWon_then_reload()
    {
        using var sp = BuildServices();
        var data   = sp.GetRequiredService<IDataRegistry>();
        var engine = sp.GetRequiredService<BattleEngine>();
        var ctx    = BuildRouteCtx(sp);
        var player = BuildCyndaquil(sp);

        // Phase 1: overworld reaches Handle
        var owEngine = sp.GetRequiredService<OverworldEngine>();
        owEngine.Tick(ctx); owEngine.Tick(ctx); // Start → Enter → Handle

        // Phase 2: inject encounter (simulates WildEncounterSystem)
        ctx.LoadWildMon("PIDGEY", 3);
        ctx.StartBattle();
        Assert.Equal(MapStatus.Done, ctx.MapStatus);

        // Phase 3: battle — KO the opponent in one turn
        var pidgeySpecies = data.Get<SpeciesData>("PIDGEY");
        var wildPidgey = new BattlePokemon(
            SpeciesId: "PIDGEY", HeldItemId: "NO_ITEM",
            Moves: ["TACKLE", "NO_MOVE", "NO_MOVE", "NO_MOVE"],
            DVs: new DVs(0, 0, 0, 0), PP: [35, 0, 0, 0],
            Happiness: 70, Level: 2,
            Status: PrimaryStatus.None, SleepCounter: 0,
            Hp: 1, MaxHp: 1,
            Attack: 5, Defense: 5, Speed: 5, SpAtk: 5, SpDef: 5,
            Type1Id: pidgeySpecies.Type1Id, Type2Id: pidgeySpecies.Type2Id);

        var state   = new BattleState(player, wildPidgey, isWild: true);
        var events  = new List<BattleEvent>();
        var outcome = engine.ExecuteTurn(state, new UseMoveAction(0), events);
        Assert.Equal(BattleOutcome.PlayerWon, outcome);

        // Phase 4: return to overworld
        ctx.ReloadMapAfterBattle();
        Assert.Equal(MapStatus.Start, ctx.MapStatus);
    }

    // -------------------------------------------------------------------------
    // 6. ROUTE_29 map loaded with correct encounter table
    // -------------------------------------------------------------------------

    [Fact]
    public void ROUTE29_loaded_with_grass_encounter_table()
    {
        using var sp = BuildServices();
        var ctx  = BuildRouteCtx(sp);
        var maps = sp.GetRequiredService<MapRegistry>();

        Assert.True(maps.TryGet("ROUTE_29", out var map) && map is not null);
        Assert.NotNull(map!.WildGrass);
        Assert.Equal(10, map.WildGrass!.DayRate);
        Assert.Equal(7, map.WildGrass.Day.Length);
        Assert.Contains(map.WildGrass.Day, s => s.SpeciesId == "PIDGEY");
    }
}
