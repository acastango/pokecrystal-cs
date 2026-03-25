namespace PokeCrystal.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using PokeCrystal.Engine.AI;
using PokeCrystal.Engine.Battle;
using PokeCrystal.Integration.Helpers;
using PokeCrystal.Schema;
using Xunit;

/// <summary>
/// Integration tests for the Gen 2 BattleEngine turn loop.
///
/// Philosophy:
///   - NORMAL-type moves on NORMAL/NORMAL mons → type effectiveness = 1.0×
///     with no TypeMatchup data needed in the stub registry.
///   - Accuracy = 0 on all test moves → never miss (no hit-check RNG).
///   - Flee/ordering scenarios use deterministic speed ratios.
///   - Status chip damage is pure arithmetic — fully deterministic.
/// </summary>
public sealed class BattleEngineTests
{
    // -------------------------------------------------------------------------
    // Common test moves (NORMAL type, Accuracy=0 = never miss)
    // -------------------------------------------------------------------------

    private static readonly MoveData Tackle = new(
        "TACKLE", "Tackle",
        Power: 35, TypeId: "NORMAL", Accuracy: 0, PP: 35,
        EffectChance: 0, EffectKey: "EFFECT_NORMAL",
        Priority: 0, Flags: MoveFlags.Contact, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData QuickAttack = new(
        "QUICK_ATTACK", "Quick Attack",
        Power: 40, TypeId: "NORMAL", Accuracy: 0, PP: 30,
        EffectChance: 0, EffectKey: "EFFECT_NORMAL",
        Priority: 1, Flags: MoveFlags.Contact, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData SleepPowder = new(
        "SLEEP_POWDER", "Sleep Powder",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 15,
        EffectChance: 0, EffectKey: "EFFECT_SLEEP",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData ThunderWave = new(
        "THUNDER_WAVE", "Thunder Wave",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 20,
        EffectChance: 0, EffectKey: "EFFECT_PARALYZE",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData WillOWisp = new(
        "WILL_O_WISP", "Will-O-Wisp",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 15,
        EffectChance: 0, EffectKey: "EFFECT_BURN",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData ToxicMove = new(
        "TOXIC", "Toxic",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 10,
        EffectChance: 0, EffectKey: "EFFECT_TOXIC",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData ConfuseRay = new(
        "CONFUSE_RAY", "Confuse Ray",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 10,
        EffectChance: 0, EffectKey: "EFFECT_CONFUSE",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    private static readonly MoveData LeechSeedMove = new(
        "LEECH_SEED", "Leech Seed",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 10,
        EffectChance: 0, EffectKey: "EFFECT_LEECH_SEED",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    // -------------------------------------------------------------------------
    // Factory helpers
    // -------------------------------------------------------------------------

    private static StubDataRegistry Db(params MoveData[] moves)
    {
        var db = new StubDataRegistry();
        foreach (var m in moves) db.Register(m);
        return db;
    }

    private static BattleEngine Engine(StubDataRegistry db, int seed = 42)
    {
        var rng    = new Random(seed);
        var types  = new TypeEffectivenessResolver(db);
        var damage = new DamageCalculator(types);
        return new BattleEngine(damage, types, db, new[] { new BasicAI(rng) }, rng);
    }

    /// <summary>NORMAL/NORMAL type, one move in slot 0, three NO_MOVE fillers.</summary>
    private static BattlePokemon Mon(
        int level, int hp, int atk, int def, int spd,
        string moveId, byte pp = 35,
        PrimaryStatus status = PrimaryStatus.None,
        byte sleepCounter = 0)
    {
        return new BattlePokemon(
            SpeciesId:    "TEST",
            HeldItemId:   "NO_ITEM",
            Moves:        [moveId, "NO_MOVE", "NO_MOVE", "NO_MOVE"],
            DVs:          new DVs(0, 0, 0, 0),
            PP:           [pp, 0, 0, 0],
            Happiness:    70,
            Level:        (byte)level,
            Status:       status,
            SleepCounter: sleepCounter,
            Hp:           hp, MaxHp: hp,
            Attack:       atk, Defense: def, Speed: spd,
            SpAtk:        atk, SpDef: def,
            Type1Id:      "NORMAL", Type2Id: "NORMAL");
    }

    // -------------------------------------------------------------------------
    // 1. Full turn — both sides deal damage and battle continues
    // -------------------------------------------------------------------------

    [Fact]
    public void Full_turn_both_sides_deal_damage_and_outcome_is_Ongoing()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TACKLE");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd: 50,  "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        var outcome = engine.ExecuteTurn(state, new UseMoveAction(0), events);

        Assert.Equal(BattleOutcome.Ongoing, outcome);
        var used = events.OfType<MoveUsedEvent>().ToList();
        Assert.Equal(2, used.Count);
        var dmg = events.OfType<DamageDealtEvent>().ToList();
        Assert.Equal(2, dmg.Count);
        Assert.All(dmg, d => Assert.True(d.Amount > 0));
        // Player went first (faster)
        Assert.True(used[0].ByPlayer);
        Assert.False(used[1].ByPlayer);
    }

    // -------------------------------------------------------------------------
    // 2. Priority ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void Priority_move_goes_first_regardless_of_speed()
    {
        var db     = Db(Tackle, QuickAttack);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "QUICK_ATTACK");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        var used = events.OfType<MoveUsedEvent>().ToList();
        Assert.True(used[0].ByPlayer, "Player with QuickAttack (priority 1) should go first.");
    }

    // -------------------------------------------------------------------------
    // 3. Speed ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void Faster_mon_goes_first_at_equal_priority()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TACKLE");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        var used = events.OfType<MoveUsedEvent>().ToList();
        Assert.True(used[0].ByPlayer, "Faster player should go first.");
    }

    // -------------------------------------------------------------------------
    // 4. Paralysis halves speed for ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void Paralysis_halves_effective_speed_for_ordering()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        // Player: spd=200 but paralyzed → effective=50; opponent: spd=100
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TACKLE",
                         status: PrimaryStatus.Paralyzed);
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd: 100, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        // The opponent must have the first MoveUsedEvent (if any).
        // If the paralysis skip fires, the player gets no MoveUsedEvent at all.
        var used = events.OfType<MoveUsedEvent>().ToList();
        if (used.Count > 0)
            Assert.False(used[0].ByPlayer, "Paralyzed player (eff. spd=50) should not beat opp spd=100.");
    }

    // -------------------------------------------------------------------------
    // 5. Sleep infliction
    // -------------------------------------------------------------------------

    [Fact]
    public void SleepPowder_inflicts_Asleep_on_opponent()
    {
        var db     = Db(SleepPowder, Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "SLEEP_POWDER");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        // Sleep infliction event must appear exactly once, targeting the opponent.
        // Note: Gen 2 — if sleep counter=1, the mon wakes and still acts this turn,
        // so Pokemon.Status may already be None by end of turn. Test the event, not state.
        var inflicted = events.OfType<StatusInflictedEvent>().ToList();
        Assert.Single(inflicted);
        Assert.False(inflicted[0].ToPlayer);
        Assert.Equal(PrimaryStatus.Asleep, inflicted[0].Status);
    }

    // -------------------------------------------------------------------------
    // 6. Burn chip: 1/8 max HP end-of-turn
    // -------------------------------------------------------------------------

    [Fact]
    public void Burned_mon_loses_1_8_maxHp_at_end_of_turn()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 80, atk: 1, def: 200, spd:  50, "TACKLE",
                         status: PrimaryStatus.Burned);
        var opp    = Mon(20, hp: 200, atk: 1, def: 200, spd: 200, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        int expected = Math.Max(1, 80 / 8); // = 10
        var chip = events.OfType<EndOfTurnDamageEvent>()
                         .FirstOrDefault(e => e.ToPlayer && e.Source == "burn");
        Assert.NotNull(chip);
        Assert.Equal(expected, chip.Amount);
    }

    // -------------------------------------------------------------------------
    // 7. Poison chip: 1/8 max HP end-of-turn
    // -------------------------------------------------------------------------

    [Fact]
    public void Poisoned_mon_loses_1_8_maxHp_at_end_of_turn()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 80, atk: 1, def: 200, spd:  50, "TACKLE",
                         status: PrimaryStatus.Poisoned);
        var opp    = Mon(20, hp: 200, atk: 1, def: 200, spd: 200, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        int expected = Math.Max(1, 80 / 8); // = 10
        var chip = events.OfType<EndOfTurnDamageEvent>()
                         .FirstOrDefault(e => e.ToPlayer && e.Source == "poison");
        Assert.NotNull(chip);
        Assert.Equal(expected, chip.Amount);
    }

    // -------------------------------------------------------------------------
    // 8. Toxic damage scales with counter each turn
    // -------------------------------------------------------------------------

    [Fact]
    public void Toxic_damage_grows_by_counter_each_turn()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 80, atk: 1, def: 200, spd:  50, "TACKLE",
                         status: PrimaryStatus.BadlyPoisoned);
        var opp    = Mon(20, hp: 200, atk: 1, def: 200, spd: 200, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        state.Player.ToxicCounter = 1;

        var ev1 = new List<BattleEvent>();
        engine.ExecuteTurn(state, new UseMoveAction(0), ev1);
        int dmg1 = ev1.OfType<EndOfTurnDamageEvent>()
                      .First(e => e.ToPlayer && e.Source == "toxic").Amount;

        var ev2 = new List<BattleEvent>();
        engine.ExecuteTurn(state, new UseMoveAction(0), ev2);
        int dmg2 = ev2.OfType<EndOfTurnDamageEvent>()
                      .First(e => e.ToPlayer && e.Source == "toxic").Amount;

        // Turn 1: 80*1/16 = 5; Turn 2: 80*2/16 = 10
        Assert.Equal(Math.Max(1, 80 * 1 / 16), dmg1);
        Assert.Equal(Math.Max(1, 80 * 2 / 16), dmg2);
        Assert.True(dmg2 > dmg1, "Toxic chip must grow each turn.");
    }

    // -------------------------------------------------------------------------
    // 9. Flee from wild — guaranteed when player is far faster
    // -------------------------------------------------------------------------

    [Fact]
    public void Flee_from_wild_succeeds_when_player_far_faster()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        // odds = 255*128/1 + 30*1 = 32670 ≥ 256 → auto-escape, no d256 roll
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 255, "TACKLE");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:   1, "TACKLE");
        var state  = new BattleState(player, opp, isWild: true);
        var events = new List<BattleEvent>();

        var outcome = engine.ExecuteTurn(state, new FleeAction(), events);

        Assert.Equal(BattleOutcome.Fled, outcome);
        Assert.Contains(events, e => e is FledEvent);
    }

    // -------------------------------------------------------------------------
    // 10. Flee from trainer — always refused
    // -------------------------------------------------------------------------

    [Fact]
    public void Flee_from_trainer_is_always_refused()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 255, "TACKLE");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:   1, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        var outcome = engine.ExecuteTurn(state, new FleeAction(), events);

        Assert.NotEqual(BattleOutcome.Fled, outcome);
        Assert.Contains(events, e => e is FleeFailed);
    }

    // -------------------------------------------------------------------------
    // 11. Faint opponent → PlayerWon
    // -------------------------------------------------------------------------

    [Fact]
    public void Fainting_opponent_returns_PlayerWon()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 999, def: 50, spd: 200, "TACKLE");
        var opp    = Mon(20, hp:   1, atk:  50, def:  1, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: true);
        var events = new List<BattleEvent>();

        var outcome = engine.ExecuteTurn(state, new UseMoveAction(0), events);

        Assert.Equal(BattleOutcome.PlayerWon, outcome);
        Assert.Contains(events, e => e is FaintedEvent f && !f.IsPlayer);
    }

    // -------------------------------------------------------------------------
    // 12. Faint player → OpponentWon
    // -------------------------------------------------------------------------

    [Fact]
    public void Fainting_player_returns_OpponentWon()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp:   1, atk:  50, def:  1, spd:  50, "TACKLE");
        var opp    = Mon(20, hp: 200, atk: 999, def: 50, spd: 200, "TACKLE");
        var state  = new BattleState(player, opp, isWild: true);
        var events = new List<BattleEvent>();

        var outcome = engine.ExecuteTurn(state, new UseMoveAction(0), events);

        Assert.Equal(BattleOutcome.OpponentWon, outcome);
        Assert.Contains(events, e => e is FaintedEvent f && f.IsPlayer);
    }

    // -------------------------------------------------------------------------
    // 13. Struggle when PP = 0
    // -------------------------------------------------------------------------

    [Fact]
    public void All_PP_zero_triggers_Struggle_with_recoil()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        // pp=0 → falls back to Struggle
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TACKLE", pp: 0);
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        // Player's move announcement should say "Struggle"
        var used = events.OfType<MoveUsedEvent>().First(e => e.ByPlayer);
        Assert.Equal("Struggle", used.MoveName);

        // Recoil event (1/4 max HP = 50)
        var recoil = events.OfType<EndOfTurnDamageEvent>()
                           .FirstOrDefault(e => e.ToPlayer && e.Source == "recoil");
        Assert.NotNull(recoil);
        Assert.Equal(Math.Max(1, 200 / 4), recoil.Amount);
    }

    // -------------------------------------------------------------------------
    // 14. Leech Seed drains target at end-of-turn
    // -------------------------------------------------------------------------

    [Fact]
    public void Leech_Seed_applies_and_drains_each_turn()
    {
        var db     = Db(LeechSeedMove, Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "LEECH_SEED");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        // Leech Seed applied to opponent
        Assert.Contains(events,
            e => e is VolatileAppliedEvent v && !v.ToPlayer
                 && v.Flag.HasFlag(VolatileStatus.LeechSeed));

        // Drain damage at end-of-turn
        int expected = Math.Max(1, 200 / 8); // = 25
        var drain = events.OfType<EndOfTurnDamageEvent>()
                          .FirstOrDefault(e => !e.ToPlayer && e.Source == "leechseed");
        Assert.NotNull(drain);
        Assert.Equal(expected, drain.Amount);
    }

    // -------------------------------------------------------------------------
    // 15. Critical hits can occur with high speed
    // -------------------------------------------------------------------------

    [Fact]
    public void High_speed_mon_produces_critical_hits()
    {
        // threshold = Clamp(254/2, 1, 255) = 127; P(crit) = 127/256 ≈ 50%.
        // Chance of 0 crits in 30 turns ≈ (129/256)^30 < 0.01%.
        var db     = Db(Tackle);
        var engine = Engine(db, seed: 0);
        var player = Mon(20, hp: 2000, atk: 10, def: 200, spd: 254, "TACKLE");
        var opp    = Mon(20, hp: 2000, atk: 10, def: 200, spd:   1, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);

        bool sawCrit = false;
        for (int t = 0; t < 30 && !sawCrit; t++)
        {
            var events = new List<BattleEvent>();
            engine.ExecuteTurn(state, new UseMoveAction(0), events);
            sawCrit = events.OfType<DamageDealtEvent>().Any(d => d.IsCritical && !d.ToPlayer);
        }

        Assert.True(sawCrit, "Expected at least one critical hit with spd=254 over 30 turns.");
    }

    // -------------------------------------------------------------------------
    // 16. Paralysis infliction
    // -------------------------------------------------------------------------

    [Fact]
    public void Thunder_Wave_inflicts_Paralyzed_on_opponent()
    {
        var db     = Db(ThunderWave, Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "THUNDER_WAVE");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        var inflicted = events.OfType<StatusInflictedEvent>().ToList();
        Assert.Single(inflicted);
        Assert.False(inflicted[0].ToPlayer);
        Assert.Equal(PrimaryStatus.Paralyzed, inflicted[0].Status);
    }

    // -------------------------------------------------------------------------
    // 17. Toxic infliction
    // -------------------------------------------------------------------------

    [Fact]
    public void Toxic_inflicts_BadlyPoisoned_on_opponent()
    {
        var db     = Db(ToxicMove, Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TOXIC");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        var inflicted = events.OfType<StatusInflictedEvent>().ToList();
        Assert.Single(inflicted);
        Assert.Equal(PrimaryStatus.BadlyPoisoned, inflicted[0].Status);
        Assert.Equal(PrimaryStatus.BadlyPoisoned, state.Opponent.Pokemon.Status);
    }

    // -------------------------------------------------------------------------
    // 18. Confusion infliction
    // -------------------------------------------------------------------------

    [Fact]
    public void Confuse_Ray_inflicts_Confused_volatile_on_opponent()
    {
        var db     = Db(ConfuseRay, Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "CONFUSE_RAY");
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        Assert.Contains(events,
            e => e is VolatileAppliedEvent v && !v.ToPlayer
                 && v.Flag.HasFlag(VolatileStatus.Confused));
        Assert.True(state.Opponent.Volatile.HasFlag(VolatileStatus.Confused));
    }

    // -------------------------------------------------------------------------
    // 19. PP is decremented after use
    // -------------------------------------------------------------------------

    [Fact]
    public void PP_is_decremented_after_using_a_move()
    {
        var db     = Db(Tackle);
        var engine = Engine(db);
        var player = Mon(20, hp: 200, atk: 50, def: 50, spd: 200, "TACKLE", pp: 10);
        var opp    = Mon(20, hp: 200, atk: 50, def: 50, spd:  50, "TACKLE");
        var state  = new BattleState(player, opp, isWild: false);
        var events = new List<BattleEvent>();

        engine.ExecuteTurn(state, new UseMoveAction(0), events);

        Assert.Equal(9, state.Player.Pokemon.PP[0]);
    }
}
