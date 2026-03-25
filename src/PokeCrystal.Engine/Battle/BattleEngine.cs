namespace PokeCrystal.Engine.Battle;

using PokeCrystal.Data;
using PokeCrystal.Schema;

/// <summary>
/// Gen 2 battle turn loop.
///
/// Orchestrates move selection, priority ordering, execution, and end-of-turn
/// effects. Pure logic — no rendering, no I/O. Registered as a singleton in L2.
///
/// Caller pattern (L13 BattleScene):
///   var state  = new BattleState(playerMon, opponentMon, isWild);
///   var events = new List&lt;BattleEvent&gt;();
///   var outcome = engine.ExecuteTurn(state, playerAction, events);
///   // iterate events to drive UI
/// </summary>
public sealed class BattleEngine
{
    private readonly IDamageCalculator         _damage;
    private readonly ITypeEffectivenessResolver _types;
    private readonly IDataRegistry             _data;
    private readonly IReadOnlyDictionary<string, IAIStrategy> _ai;
    private readonly Random _rng;

    // Sentinel move used when all PP are depleted.
    // Source: engine/battle/effect_commands.asm BattleCommand_Struggle
    private static readonly MoveData Struggle = new(
        "STRUGGLE", "Struggle",
        Power: 50, TypeId: "NORMAL", Accuracy: 0, PP: 1,
        EffectChance: 0, EffectKey: "EFFECT_STRUGGLE",
        Priority: 0, Flags: MoveFlags.Contact, Target: MoveTarget.SelectedOpponent);

    // Filler for missing move slots so AI index arithmetic stays consistent.
    private static readonly MoveData NoMove = new(
        "NO_MOVE", "---",
        Power: 0, TypeId: "NORMAL", Accuracy: 0, PP: 0,
        EffectChance: 0, EffectKey: "EFFECT_NORMAL",
        Priority: 0, Flags: MoveFlags.None, Target: MoveTarget.SelectedOpponent);

    public BattleEngine(
        IDamageCalculator damage,
        ITypeEffectivenessResolver types,
        IDataRegistry data,
        IEnumerable<IAIStrategy> ai,
        Random? rng = null)
    {
        _damage = damage;
        _types  = types;
        _data   = data;
        _ai     = ai.ToDictionary(s => s.StrategyKey);
        _rng    = rng ?? Random.Shared;
    }

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a transient BattlePokemon from a party member + its species record.
    /// Called by the scene layer when entering battle.
    /// </summary>
    public static BattlePokemon ToBattlePokemon(PartyPokemon party, SpeciesData species) =>
        new(SpeciesId:    party.Base.SpeciesId,
            HeldItemId:   party.Base.HeldItemId,
            Moves:        party.Base.Moves,
            DVs:          party.Base.DVs,
            PP:           (byte[])party.Base.PP.Clone(),
            Happiness:    party.Base.Happiness,
            Level:        party.Base.Level,
            Status:       party.Status,
            SleepCounter: party.SleepCounter,
            Hp:           party.CurrentHp,
            MaxHp:        party.MaxHp,
            Attack:       party.Attack,
            Defense:      party.Defense,
            Speed:        party.Speed,
            SpAtk:        party.SpAtk,
            SpDef:        party.SpDef,
            Type1Id:      species.Type1Id,
            Type2Id:      species.Type2Id);

    // -------------------------------------------------------------------------
    // Turn loop
    // -------------------------------------------------------------------------

    /// <summary>
    /// Execute one full turn. Appends ordered events and returns the outcome.
    /// Events drive animation, text, and HP bar updates without re-running logic.
    /// </summary>
    public BattleOutcome ExecuteTurn(
        BattleState state,
        BattleAction playerAction,
        List<BattleEvent> events)
    {
        state.Turn++;

        // Flee attempt — opponent still moves if it fails
        if (playerAction is FleeAction)
        {
            if (TryFlee(state, events)) return BattleOutcome.Fled;
            MoveData oppMove = ResolveAIMove(state, out int oppSlot);
            ExecuteMove(state, state.Opponent, state.Player, false, oppMove, oppSlot, events);
            if (!state.Player.IsAlive)
            {
                events.Add(new FaintedEvent(true));
                return BattleOutcome.OpponentWon;
            }
            return FinishTurn(state, events);
        }

        // Both sides select moves
        MoveData playerMove   = ResolvePlayerMove(state, playerAction, out int playerSlot);
        MoveData opponentMove = ResolveAIMove(state, out int opponentSlot);

        bool playerFirst = DetermineOrder(state, playerMove, opponentMove);

        var (fAtk, fDef, fMove, fSlot, fIsPlayer) = playerFirst
            ? (state.Player,   state.Opponent, playerMove,   playerSlot,   true)
            : (state.Opponent, state.Player,   opponentMove, opponentSlot, false);
        var (sAtk, sDef, sMove, sSlot, sIsPlayer) = playerFirst
            ? (state.Opponent, state.Player,   opponentMove, opponentSlot, false)
            : (state.Player,   state.Opponent, playerMove,   playerSlot,   true);

        // First mover
        if (fAtk.IsAlive)
        {
            ExecuteMove(state, fAtk, fDef, fIsPlayer, fMove, fSlot, events);
            if (!fDef.IsAlive)
            {
                events.Add(new FaintedEvent(!fIsPlayer));
                return fIsPlayer ? BattleOutcome.PlayerWon : BattleOutcome.OpponentWon;
            }
        }

        // Second mover
        if (sAtk.IsAlive)
        {
            ExecuteMove(state, sAtk, sDef, sIsPlayer, sMove, sSlot, events);
            if (!sDef.IsAlive)
            {
                events.Add(new FaintedEvent(!sIsPlayer));
                return sIsPlayer ? BattleOutcome.PlayerWon : BattleOutcome.OpponentWon;
            }
        }

        return FinishTurn(state, events);
    }

    private BattleOutcome FinishTurn(BattleState state, List<BattleEvent> events)
    {
        ApplyEndOfTurn(state, events);
        if (!state.Player.IsAlive)
        {
            events.Add(new FaintedEvent(true));
            return BattleOutcome.OpponentWon;
        }
        if (!state.Opponent.IsAlive)
        {
            events.Add(new FaintedEvent(false));
            return BattleOutcome.PlayerWon;
        }
        return BattleOutcome.Ongoing;
    }

    // -------------------------------------------------------------------------
    // Move resolution
    // -------------------------------------------------------------------------

    private MoveData ResolvePlayerMove(BattleState state, BattleAction action, out int slot)
    {
        if (action is UseMoveAction use)
        {
            slot = use.SlotIndex;
            var mon = state.Player.Pokemon;
            if ((uint)slot < (uint)mon.Moves.Length &&
                (uint)slot < (uint)mon.PP.Length   &&
                mon.PP[slot] > 0                   &&
                _data.TryGet<MoveData>(mon.Moves[slot], out var mv) && mv is not null)
                return mv;
        }
        slot = 0;
        return Struggle;
    }

    private MoveData ResolveAIMove(BattleState state, out int slot)
    {
        var mon = state.Opponent.Pokemon;

        // Build a 4-slot array (NoMove fills missing/invalid slots).
        // BasicAI filters by PP and "NO_MOVE" Id, so the filler is handled correctly.
        var arr = new MoveData[4];
        for (int i = 0; i < 4; i++)
        {
            arr[i] = (i < mon.Moves.Length &&
                      _data.TryGet<MoveData>(mon.Moves[i], out var mv) && mv is not null)
                     ? mv : NoMove;
        }

        var ctx      = state.AsContext(false);
        var strategy = _ai.GetValueOrDefault("basic");
        slot = strategy?.SelectMove(ctx, mon, state.Player.Pokemon, arr) ?? 0;
        slot = Math.Clamp(slot, 0, 3);

        if (arr[slot].Id == "NO_MOVE" ||
            (uint)slot >= (uint)mon.PP.Length ||
            mon.PP[slot] == 0)
        {
            slot = 0;
            return Struggle;
        }
        return arr[slot];
    }

    private bool DetermineOrder(BattleState state, MoveData playerMove, MoveData opponentMove)
    {
        if (playerMove.Priority != opponentMove.Priority)
            return playerMove.Priority > opponentMove.Priority;

        // Paralysis halves effective speed for ordering.
        // Source: engine/battle/core.asm GetMovePriority
        int ps = state.Player.Pokemon.Speed;
        int os = state.Opponent.Pokemon.Speed;
        if (state.Player.Pokemon.Status   == PrimaryStatus.Paralyzed) ps = Math.Max(1, ps / 4);
        if (state.Opponent.Pokemon.Status == PrimaryStatus.Paralyzed) os = Math.Max(1, os / 4);

        if (ps != os) return ps > os;
        return _rng.Next(2) == 0;
    }

    // -------------------------------------------------------------------------
    // Move execution
    // -------------------------------------------------------------------------

    private void ExecuteMove(
        BattleState state,
        CombatantState attacker, CombatantState defender,
        bool attackerIsPlayer,
        MoveData move, int moveSlot,
        List<BattleEvent> events)
    {
        if (!CanMove(attacker, move, attackerIsPlayer, events)) return;

        events.Add(new MoveUsedEvent(attackerIsPlayer, move.Name));

        // Deduct PP (Struggle has no PP to deduct)
        if (move.Id != "STRUGGLE" && (uint)moveSlot < (uint)attacker.Pokemon.PP.Length)
        {
            var pp = (byte[])attacker.Pokemon.PP.Clone();
            if (pp[moveSlot] > 0) pp[moveSlot]--;
            attacker.Pokemon = attacker.Pokemon with { PP = pp };
        }

        var ctx = state.AsContext(attackerIsPlayer);

        // Accuracy check (0 = never misses — Swift, Toxic, etc.)
        if (move.Accuracy > 0 && !HitCheck(ctx, move))
        {
            events.Add(new MoveMissedEvent(attackerIsPlayer));
            return;
        }

        // Damage phase
        if (move.Power > 0)
        {
            bool  isCrit        = RollCrit(attacker.Pokemon.Speed);
            float effectiveness = _types.GetMultiplier(
                move.TypeId, defender.Pokemon.Type1Id, defender.Pokemon.Type2Id, ctx);
            int dmg = _damage.Calculate(ctx, attacker.Pokemon, defender.Pokemon, move, isCrit);

            if (dmg > 0)
            {
                defender.TakeDamage(dmg);
                events.Add(new DamageDealtEvent(!attackerIsPlayer, dmg, isCrit, effectiveness));
                if (!defender.IsAlive) return;  // faint handled by caller
            }

            // Struggle recoil: 1/4 attacker max HP
            if (move.Id == "STRUGGLE")
            {
                int recoil = Math.Max(1, attacker.Pokemon.MaxHp / 4);
                attacker.TakeDamage(recoil);
                events.Add(new EndOfTurnDamageEvent(attackerIsPlayer, recoil, "recoil"));
            }
        }

        ApplyEffect(state, attacker, defender, attackerIsPlayer, move, events);

        // Hyper Beam / Giga Impact: must recharge next turn
        if (move.EffectKey == "EFFECT_RECHARGE")
            attacker.Volatile |= VolatileStatus.Recharge;
    }

    // -------------------------------------------------------------------------
    // Pre-move checks
    // Source: engine/battle/effect_commands.asm BattleCommand_CheckSleepFaint etc.
    // -------------------------------------------------------------------------

    private bool CanMove(
        CombatantState mon, MoveData move, bool isPlayer, List<BattleEvent> events)
    {
        // Recharge (Hyper Beam — skip turn and clear flag)
        if (mon.Volatile.HasFlag(VolatileStatus.Recharge))
        {
            mon.Volatile &= ~VolatileStatus.Recharge;
            return false;
        }

        // Flinch (set by a faster move earlier this turn)
        if (mon.Volatile.HasFlag(VolatileStatus.Flinched))
        {
            mon.Volatile &= ~VolatileStatus.Flinched;
            return false;
        }

        // Sleep: tick counter; wake when it reaches 0
        if (mon.Pokemon.Status == PrimaryStatus.Asleep)
        {
            mon.TickSleep();
            if (mon.Pokemon.Status == PrimaryStatus.Asleep) return false;
            events.Add(new StatusCuredEvent(isPlayer, PrimaryStatus.Asleep));
            // Woke up this turn — still gets to move in Gen 2
        }

        // Freeze: thaw on fire-type move used against self, or 10% random thaw per turn.
        // Source: engine/battle/effect_commands.asm BattleCommand_CheckFrozen
        if (mon.Pokemon.Status == PrimaryStatus.Frozen)
        {
            bool thaw = move.TypeId == "FIRE" || _rng.Next(10) == 0;
            if (!thaw) return false;
            mon.SetStatus(PrimaryStatus.None);
            events.Add(new StatusCuredEvent(isPlayer, PrimaryStatus.Frozen));
        }

        // Paralysis: 25% chance to be fully paralyzed this turn
        if (mon.Pokemon.Status == PrimaryStatus.Paralyzed && _rng.Next(4) == 0) return false;

        // Confusion: decrement counter, 50% chance to hurt self
        if (mon.Volatile.HasFlag(VolatileStatus.Confused))
        {
            mon.ConfusionTurns--;
            if (mon.ConfusionTurns <= 0)
            {
                mon.ConfusionTurns = 0;
                mon.Volatile &= ~VolatileStatus.Confused;
                // snapped out — still acts this turn
            }
            else if (_rng.Next(2) == 0)
            {
                // Hurt self: typeless 40-power, uses raw Atk/Def (no stage modifiers — Gen 2 quirk)
                // Source: engine/battle/effect_commands.asm BattleCommand_HurtConfusedMon
                int selfDmg = Math.Max(1,
                    (mon.Pokemon.Level * 2 / 5 + 2) * 40
                    * mon.Pokemon.Attack / Math.Max(1, mon.Pokemon.Defense) / 50 + 2);
                mon.TakeDamage(selfDmg);
                events.Add(new EndOfTurnDamageEvent(isPlayer, selfDmg, "confusion"));
                return false;
            }
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // Accuracy
    // Source: engine/battle/effect_commands.asm BattleCommand_CheckHit
    // -------------------------------------------------------------------------

    private bool HitCheck(IBattleContext ctx, MoveData move)
    {
        // Final hit chance = moveAccuracy × accStage(attacker) / evaStage(defender)
        // Both stages use the same ASM table; see StatCalculator.AccuracyStageMultipliers.
        int atkAcc   = StatCalculator.ApplyAccuracyStage(move.Accuracy, ctx.AttackerStages.Accuracy);
        int defEva   = StatCalculator.ApplyAccuracyStage(100,           ctx.DefenderStages.Evasion);
        int hitChance = defEva == 0 ? atkAcc : atkAcc * 100 / defEva;
        return _rng.Next(100) < hitChance;
    }

    // -------------------------------------------------------------------------
    // Critical hit
    // Source: engine/battle/effect_commands.asm CriticalHitChance / QuickCritRate
    // Normal moves: threshold = floor(speed / 2), rolled against d256.
    // -------------------------------------------------------------------------

    private bool RollCrit(int speed)
    {
        int threshold = Math.Clamp(speed / 2, 1, 255);
        return _rng.Next(256) < threshold;
    }

    // -------------------------------------------------------------------------
    // Effect dispatch
    // -------------------------------------------------------------------------

    private void ApplyEffect(
        BattleState state,
        CombatantState attacker, CombatantState defender,
        bool attackerIsPlayer,
        MoveData move, List<BattleEvent> events)
    {
        // Damaging moves with a secondary effect: roll effectChance
        if (move.Power > 0 && move.EffectChance > 0 && _rng.Next(100) >= move.EffectChance)
            return;

        switch (move.EffectKey)
        {
            // --- Primary status ---
            case "EFFECT_SLEEP":
                TryInflictStatus(defender, !attackerIsPlayer,
                    PrimaryStatus.Asleep, events, (byte)(_rng.Next(3) + 1));
                break;
            case "EFFECT_POISON":
            case "EFFECT_POISON_HIT":
                TryInflictStatus(defender, !attackerIsPlayer, PrimaryStatus.Poisoned, events);
                break;
            case "EFFECT_TOXIC":
                TryInflictStatus(defender, !attackerIsPlayer, PrimaryStatus.BadlyPoisoned, events);
                break;
            case "EFFECT_BURN":
            case "EFFECT_BURN_HIT":
                TryInflictStatus(defender, !attackerIsPlayer, PrimaryStatus.Burned, events);
                break;
            case "EFFECT_FREEZE":
            case "EFFECT_FREEZE_HIT":
                TryInflictStatus(defender, !attackerIsPlayer, PrimaryStatus.Frozen, events);
                break;
            case "EFFECT_PARALYZE":
            case "EFFECT_PARALYZE_HIT":
                TryInflictStatus(defender, !attackerIsPlayer, PrimaryStatus.Paralyzed, events);
                break;

            // --- Confusion ---
            case "EFFECT_CONFUSE":
            case "EFFECT_CONFUSE_HIT":
                TryInflictConfusion(defender, !attackerIsPlayer, events);
                break;

            // --- Flinch ---
            case "EFFECT_FLINCH":
            case "EFFECT_FLINCH_HIT":
                defender.Volatile |= VolatileStatus.Flinched;
                break;

            // --- Stat stages: self ---
            case "EFFECT_ATTACK_UP_1":          StageChange(attacker,  attackerIsPlayer, StatType.Attack,  +1, events); break;
            case "EFFECT_ATTACK_UP_2":          StageChange(attacker,  attackerIsPlayer, StatType.Attack,  +2, events); break;
            case "EFFECT_DEFENSE_UP_1":         StageChange(attacker,  attackerIsPlayer, StatType.Defense, +1, events); break;
            case "EFFECT_DEFENSE_UP_2":         StageChange(attacker,  attackerIsPlayer, StatType.Defense, +2, events); break;
            case "EFFECT_SPEED_UP_1":           StageChange(attacker,  attackerIsPlayer, StatType.Speed,   +1, events); break;
            case "EFFECT_SPEED_UP_2":           StageChange(attacker,  attackerIsPlayer, StatType.Speed,   +2, events); break;
            case "EFFECT_SPECIAL_ATTACK_UP_1":  StageChange(attacker,  attackerIsPlayer, StatType.SpAtk,   +1, events); break;
            case "EFFECT_SPECIAL_ATTACK_UP_2":  StageChange(attacker,  attackerIsPlayer, StatType.SpAtk,   +2, events); break;
            case "EFFECT_SPECIAL_DEFENSE_UP_1": StageChange(attacker,  attackerIsPlayer, StatType.SpDef,   +1, events); break;
            case "EFFECT_SPECIAL_DEFENSE_UP_2": StageChange(attacker,  attackerIsPlayer, StatType.SpDef,   +2, events); break;

            // --- Stat stages: opponent ---
            case "EFFECT_ATTACK_DOWN_1":          StageChange(defender, !attackerIsPlayer, StatType.Attack,  -1, events); break;
            case "EFFECT_ATTACK_DOWN_2":          StageChange(defender, !attackerIsPlayer, StatType.Attack,  -2, events); break;
            case "EFFECT_DEFENSE_DOWN_1":         StageChange(defender, !attackerIsPlayer, StatType.Defense, -1, events); break;
            case "EFFECT_DEFENSE_DOWN_2":         StageChange(defender, !attackerIsPlayer, StatType.Defense, -2, events); break;
            case "EFFECT_SPEED_DOWN_1":           StageChange(defender, !attackerIsPlayer, StatType.Speed,   -1, events); break;
            case "EFFECT_SPEED_DOWN_2":           StageChange(defender, !attackerIsPlayer, StatType.Speed,   -2, events); break;
            case "EFFECT_SPECIAL_ATTACK_DOWN_1":  StageChange(defender, !attackerIsPlayer, StatType.SpAtk,   -1, events); break;
            case "EFFECT_SPECIAL_ATTACK_DOWN_2":  StageChange(defender, !attackerIsPlayer, StatType.SpAtk,   -2, events); break;
            case "EFFECT_SPECIAL_DEFENSE_DOWN_1": StageChange(defender, !attackerIsPlayer, StatType.SpDef,   -1, events); break;
            case "EFFECT_SPECIAL_DEFENSE_DOWN_2": StageChange(defender, !attackerIsPlayer, StatType.SpDef,   -2, events); break;

            // --- Accuracy/evasion stages ---
            case "EFFECT_ACCURACY_DOWN_1":
            {
                var s = defender.Stages;
                int n = Math.Clamp(s.Accuracy - 1, StatStages.Min, StatStages.Max);
                defender.Stages = s with { Accuracy = n };
                break;
            }
            case "EFFECT_EVASION_UP_1":
            {
                var s = attacker.Stages;
                int n = Math.Clamp(s.Evasion + 1, StatStages.Min, StatStages.Max);
                attacker.Stages = s with { Evasion = n };
                break;
            }

            // --- Heal self 50% ---
            case "EFFECT_HEAL":
            {
                int amount = Math.Max(1, attacker.Pokemon.MaxHp / 2);
                attacker.Heal(amount);
                events.Add(new HealedEvent(attackerIsPlayer, amount));
                break;
            }

            // --- Leech Seed ---
            case "EFFECT_LEECH_SEED":
                if (!defender.Volatile.HasFlag(VolatileStatus.LeechSeed))
                {
                    defender.Volatile |= VolatileStatus.LeechSeed;
                    events.Add(new VolatileAppliedEvent(!attackerIsPlayer, VolatileStatus.LeechSeed));
                }
                break;

            // EFFECT_NORMAL, EFFECT_EXPLOSION, EFFECT_RECHARGE, EFFECT_STRUGGLE:
            // all handled in the damage phase or turn loop — nothing extra here.
        }
    }

    private void TryInflictStatus(
        CombatantState target, bool targetIsPlayer,
        PrimaryStatus status, List<BattleEvent> events,
        byte sleepTurns = 0)
    {
        if (target.Pokemon.Status != PrimaryStatus.None) return;
        if (target.SafeguardTurns > 0) return;

        target.SetStatus(status, status == PrimaryStatus.Asleep ? sleepTurns : (byte)0);
        if (status == PrimaryStatus.BadlyPoisoned) target.ToxicCounter = 1;
        events.Add(new StatusInflictedEvent(targetIsPlayer, status));
    }

    private void TryInflictConfusion(
        CombatantState target, bool targetIsPlayer, List<BattleEvent> events)
    {
        if (target.Volatile.HasFlag(VolatileStatus.Confused)) return;
        target.ConfusionTurns = _rng.Next(2) + 2;  // 2–3 turns (Gen 2)
        target.Volatile |= VolatileStatus.Confused;
        events.Add(new VolatileAppliedEvent(targetIsPlayer, VolatileStatus.Confused));
    }

    private static void StageChange(
        CombatantState target, bool targetIsPlayer,
        StatType stat, int delta, List<BattleEvent> events)
    {
        int actual = target.ChangeStage(stat, delta);
        if (actual != 0)
            events.Add(new StatStageChangedEvent(targetIsPlayer, stat, actual));
    }

    // -------------------------------------------------------------------------
    // Flee
    // Source: engine/battle/effect_commands.asm TryRunFromBattle
    // Formula: oddsToRun = (playerSpeed * 128 / opponentSpeed) + 30 * attempts
    //          Automatic escape if oddsToRun >= 256; else roll d256.
    // -------------------------------------------------------------------------

    private bool TryFlee(BattleState state, List<BattleEvent> events)
    {
        if (!state.IsWild)
        {
            events.Add(new FleeFailed());
            return false;
        }

        state.FleeAttempts++;
        int ps   = state.Player.Pokemon.Speed;
        int os   = state.Opponent.Pokemon.Speed;
        int odds = ps * 128 / Math.Max(1, os) + 30 * state.FleeAttempts;

        if (odds >= 256 || _rng.Next(256) < odds)
        {
            events.Add(new FledEvent());
            return true;
        }
        events.Add(new FleeFailed());
        return false;
    }

    // -------------------------------------------------------------------------
    // End-of-turn effects
    // Source: engine/battle/effect_commands.asm EndOfTurn
    // -------------------------------------------------------------------------

    private void ApplyEndOfTurn(BattleState state, List<BattleEvent> events)
    {
        TickSideConditions(state.Player);
        TickSideConditions(state.Opponent);

        // Burn: 1/8 max HP
        ChipDamage(state.Player,   true,  PrimaryStatus.Burned, 8, "burn",   events);
        ChipDamage(state.Opponent, false, PrimaryStatus.Burned, 8, "burn",   events);

        // Poison / Badly Poisoned
        PoisonChip(state.Player,   true,  events);
        PoisonChip(state.Opponent, false, events);

        // Sandstorm chip (Rock/Ground/Steel immune)
        if (state.Weather == Weather.Sandstorm)
        {
            SandstormChip(state.Player,   true,  events);
            SandstormChip(state.Opponent, false, events);
            if (state.WeatherTurns > 0 && --state.WeatherTurns == 0)
                state.Weather = Weather.None;
        }

        // Leech Seed: drain from seeded mon, heal the other
        LeechSeedChip(state, drainedIsPlayer: true,  events);
        LeechSeedChip(state, drainedIsPlayer: false, events);
    }

    private static void TickSideConditions(CombatantState c)
    {
        if (c.SafeguardTurns > 0 && --c.SafeguardTurns == 0)
            c.Side &= ~SideCondition.Safeguard;
        if (c.ScreenTurns > 0 && --c.ScreenTurns == 0)
            c.Side &= ~(SideCondition.LightScreen | SideCondition.Reflect);
    }

    private static void ChipDamage(
        CombatantState c, bool isPlayer,
        PrimaryStatus status, int divisor, string source,
        List<BattleEvent> events)
    {
        if (!c.IsAlive || c.Pokemon.Status != status) return;
        int dmg = Math.Max(1, c.Pokemon.MaxHp / divisor);
        c.TakeDamage(dmg);
        events.Add(new EndOfTurnDamageEvent(isPlayer, dmg, source));
    }

    private static void PoisonChip(CombatantState c, bool isPlayer, List<BattleEvent> events)
    {
        if (!c.IsAlive) return;
        if (c.Pokemon.Status == PrimaryStatus.Poisoned)
        {
            int dmg = Math.Max(1, c.Pokemon.MaxHp / 8);
            c.TakeDamage(dmg);
            events.Add(new EndOfTurnDamageEvent(isPlayer, dmg, "poison"));
        }
        else if (c.Pokemon.Status == PrimaryStatus.BadlyPoisoned)
        {
            int dmg = Math.Max(1, c.Pokemon.MaxHp * c.ToxicCounter / 16);
            c.ToxicCounter = Math.Min(15, c.ToxicCounter + 1);
            c.TakeDamage(dmg);
            events.Add(new EndOfTurnDamageEvent(isPlayer, dmg, "toxic"));
        }
    }

    private static void SandstormChip(CombatantState c, bool isPlayer, List<BattleEvent> events)
    {
        if (!c.IsAlive) return;
        if (c.Pokemon.Type1Id is "ROCK" or "GROUND" or "STEEL") return;
        if (c.Pokemon.Type2Id is "ROCK" or "GROUND" or "STEEL") return;
        int dmg = Math.Max(1, c.Pokemon.MaxHp / 8);
        c.TakeDamage(dmg);
        events.Add(new EndOfTurnDamageEvent(isPlayer, dmg, "sandstorm"));
    }

    private static void LeechSeedChip(
        BattleState state, bool drainedIsPlayer, List<BattleEvent> events)
    {
        var drained = drainedIsPlayer ? state.Player   : state.Opponent;
        var healed  = drainedIsPlayer ? state.Opponent : state.Player;
        if (!drained.IsAlive || !drained.Volatile.HasFlag(VolatileStatus.LeechSeed)) return;
        int dmg = Math.Max(1, drained.Pokemon.MaxHp / 8);
        drained.TakeDamage(dmg);
        events.Add(new EndOfTurnDamageEvent(drainedIsPlayer, dmg, "leechseed"));
        if (healed.IsAlive) healed.Heal(dmg);
    }
}
