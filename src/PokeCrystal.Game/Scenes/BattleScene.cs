namespace PokeCrystal.Game.Scenes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Data;
using PokeCrystal.Engine.Battle;
using PokeCrystal.Schema;
using PokeCrystal.World;
using XnaGameTime = Microsoft.Xna.Framework.GameTime;

/// <summary>
/// Battle scene — L13. Receives a BattleSetup from OverworldScene, builds
/// BattleState, drives the BattleEngine turn loop, drains the event queue
/// (L12 will animate each event), then syncs party HP/status and returns
/// to the overworld.
///
/// Circular-DI note: OverworldScene injects BattleScene; BattleScene must
/// not inject OverworldScene back. It resolves it lazily via IServiceProvider
/// to avoid a constructor cycle that would throw at startup.
/// </summary>
public sealed class BattleScene : IScene
{
    private enum Phase { MoveSelect, Resolving, Done }

    private readonly SceneManager    _scenes;
    private readonly WorldContext    _ctx;
    private readonly BattleEngine    _engine;
    private readonly IStatCalculator _stats;
    private readonly IDataRegistry   _data;
    private readonly IInputProvider  _input;
    private readonly IServiceProvider _sp;
    private readonly GameRenderer    _renderer;

    /// <summary>Set by OverworldScene before calling SceneManager.Transition.</summary>
    public BattleSetup? Setup { get; set; }

    private BattleState?               _state;
    private readonly List<BattleEvent> _eventQueue = new();
    private BattleOutcome _outcome;
    private Phase         _phase;
    private int           _cursor;        // move slot 0–3
    private string        _eventText = ""; // last resolved event — shown during Resolving

    public BattleScene(
        SceneManager     scenes,
        WorldContext     ctx,
        BattleEngine     engine,
        IStatCalculator  stats,
        IDataRegistry    data,
        IInputProvider   input,
        IServiceProvider sp,
        GameRenderer     renderer)
    {
        _scenes   = scenes;
        _ctx      = ctx;
        _engine   = engine;
        _stats    = stats;
        _data     = data;
        _input    = input;
        _sp       = sp;
        _renderer = renderer;
    }

    // -------------------------------------------------------------------------
    // IScene
    // -------------------------------------------------------------------------

    public void OnEnter()
    {
        if (Setup is null) return;

        var party = _ctx.Pokemon.Party;
        if (party.Count == 0) return;

        var playerSpecies = _data.Get<SpeciesData>(party[0].Base.SpeciesId);
        var playerMon     = BattleEngine.ToBattlePokemon(party[0], playerSpecies);

        var opponentMon = Setup.IsWild
            ? BuildWildMon(Setup.WildSpeciesId!, Setup.WildLevel)
            : BuildTrainerMon(Setup.TrainerId!);

        _state     = new BattleState(playerMon, opponentMon, Setup.IsWild);
        _eventQueue.Clear();
        _outcome   = BattleOutcome.Ongoing;
        _phase     = Phase.MoveSelect;
        _cursor    = 0;
        _eventText = "";
    }

    public void OnExit()
    {
        Setup  = null;
        _state = null;
    }

    public void Update(XnaGameTime gameTime)
    {
        switch (_phase)
        {
            case Phase.MoveSelect: UpdateMoveSelect(); break;
            case Phase.Resolving:  UpdateResolving();  break;
            case Phase.Done:       UpdateDone();       break;
        }
    }

    public void Draw(SpriteBatch sb)
    {
        const int W        = 480;
        const int H        = 432;
        const int DivY     = 210;  // divider between opponent and player zones
        const int DivH     = 12;
        const int BarW     = 220;
        const int BarH     = 14;
        const int MoveMenuY = 308;  // bottom move-select overlay top

        // ---- Backgrounds ----
        _renderer.FillRect(sb, 0, 0, W, DivY, GameRenderer.BgDark);
        _renderer.FillRect(sb, 0, DivY, W, DivH, GameRenderer.Divider);
        _renderer.FillRect(sb, 0, DivY + DivH, W, H - DivY - DivH, GameRenderer.BgPanel);

        if (_state is null) return;

        var opp = _state.Opponent.Pokemon;
        var ply = _state.Player.Pokemon;

        // ---- Opponent (top half) ----
        string oppLabel = $"{opp.SpeciesId}  Lv.{opp.Level}";
        _renderer.DrawText(sb, oppLabel, 20, 20, GameRenderer.TextMain);
        if (opp.Status != PrimaryStatus.None)
            _renderer.DrawText(sb, $"[{StatusShort(opp.Status)}]", 280, 20, GameRenderer.TextDim);
        _renderer.DrawHpBar(sb, 20, 50, BarW, BarH, opp.Hp, opp.MaxHp);
        _renderer.DrawText(sb, $"{opp.Hp} / {opp.MaxHp}", 20, 70, GameRenderer.TextDim);

        // ---- Player (bottom half) ----
        string plyLabel = $"{ply.SpeciesId}  Lv.{ply.Level}";
        _renderer.DrawText(sb, plyLabel, 20, DivY + DivH + 10, GameRenderer.TextMain);
        if (ply.Status != PrimaryStatus.None)
            _renderer.DrawText(sb, $"[{StatusShort(ply.Status)}]", 280, DivY + DivH + 10, GameRenderer.TextDim);
        _renderer.DrawHpBar(sb, 20, DivY + DivH + 40, BarW, BarH, ply.Hp, ply.MaxHp);
        _renderer.DrawText(sb, $"{ply.Hp} / {ply.MaxHp}", 20, DivY + DivH + 60, GameRenderer.TextDim);

        // ---- Phase-specific overlays ----
        switch (_phase)
        {
            case Phase.MoveSelect:
                DrawMoveMenu(sb, ply, MoveMenuY, W, H);
                break;

            case Phase.Resolving:
                _renderer.FillRect(sb, 0, MoveMenuY, W, H - MoveMenuY, GameRenderer.BgDark);
                _renderer.DrawText(sb, _eventText, 20, MoveMenuY + 16, GameRenderer.TextMain);
                break;

            case Phase.Done:
                _renderer.FillRect(sb, 0, MoveMenuY, W, H - MoveMenuY, GameRenderer.BgDark);
                string doneMsg = _outcome switch
                {
                    BattleOutcome.PlayerWon    => "You won!",
                    BattleOutcome.OpponentWon  => "You lost...",
                    BattleOutcome.Fled         => "Got away safely!",
                    _                          => "Battle over.",
                };
                _renderer.DrawText(sb, doneMsg, 20, MoveMenuY + 12, GameRenderer.TextMain);
                _renderer.DrawText(sb, "Press Z to continue", 20, MoveMenuY + 44, GameRenderer.TextDim);
                break;
        }
    }

    private void DrawMoveMenu(SpriteBatch sb, BattlePokemon ply, int menuY, int w, int h)
    {
        _renderer.FillRect(sb, 0, menuY, w, h - menuY, GameRenderer.BgDark);

        // 2×2 grid: slots 0,1 (top row) / 2,3 (bottom row)
        int[] xs = [20, 250, 20, 250];
        int[] ys = [menuY + 14, menuY + 14, menuY + 60, menuY + 60];

        for (int i = 0; i < 4; i++)
        {
            var moveId = ply.Moves[i];
            bool isEmpty = moveId == "NO_MOVE";
            bool selected = i == _cursor;

            string label = isEmpty ? "-" : moveId.Replace('_', ' ');
            var color    = selected ? GameRenderer.Cursor
                         : isEmpty  ? GameRenderer.TextDim
                         :            GameRenderer.TextMain;

            string prefix = selected ? "> " : "  ";
            _renderer.DrawText(sb, prefix + label, xs[i], ys[i], color);

            if (!isEmpty)
            {
                string ppText = $"PP {ply.PP[i],2}";
                _renderer.DrawText(sb, ppText, xs[i] + 150, ys[i], GameRenderer.TextDim);
            }
        }

        _renderer.DrawText(sb, "Z:use  X:flee  arrows:navigate", 20, h - 22, GameRenderer.TextDim);
    }

    // -------------------------------------------------------------------------
    // Phase: MoveSelect — 2×2 move grid (slot 0=TL, 1=TR, 2=BL, 3=BR)
    // -------------------------------------------------------------------------

    private void UpdateMoveSelect()
    {
        if (_state is null) { _phase = Phase.Done; return; }

        // Vertical pair swap: 0↔2, 1↔3
        if (_input.IsPressed(GameAction.MoveUp) || _input.IsPressed(GameAction.MoveDown))
            _cursor = (_cursor + 2) % 4;
        // Horizontal swap: 0↔1, 2↔3
        else if (_input.IsPressed(GameAction.MoveLeft) || _input.IsPressed(GameAction.MoveRight))
            _cursor ^= 1;

        if (_input.IsPressed(GameAction.Confirm))
            ExecutePlayerAction(new UseMoveAction(_cursor));
        else if (_input.IsPressed(GameAction.Cancel))
            ExecutePlayerAction(new FleeAction());
    }

    // -------------------------------------------------------------------------
    // Phase: Resolving — drain one event per frame (L12 adds per-event delays)
    // -------------------------------------------------------------------------

    private void UpdateResolving()
    {
        if (_eventQueue.Count > 0)
        {
            _eventText = EventToText(_eventQueue[0]);
            _eventQueue.RemoveAt(0);
            return;
        }

        _phase = _outcome == BattleOutcome.Ongoing ? Phase.MoveSelect : Phase.Done;
    }

    // -------------------------------------------------------------------------
    // Phase: Done — wait for Confirm, sync party, return to overworld
    // -------------------------------------------------------------------------

    private void UpdateDone()
    {
        if (_input.IsPressed(GameAction.Confirm))
            ReturnToOverworld();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ExecutePlayerAction(BattleAction action)
    {
        if (_state is null) return;
        _eventQueue.Clear();
        _outcome = _engine.ExecuteTurn(_state, action, _eventQueue);
        _phase   = Phase.Resolving;
    }

    private void ReturnToOverworld()
    {
        if (_state is not null)
            SyncPartyAfterBattle();
        _ctx.ReloadMapAfterBattle();
        _scenes.Transition(_sp.GetRequiredService<OverworldScene>());
    }

    private void SyncPartyAfterBattle()
    {
        var party = _ctx.Pokemon.Party;
        if (party.Count == 0 || _state is null) return;
        var bp  = _state.Player.Pokemon;
        var old = party[0];
        party[0] = old with
        {
            CurrentHp    = bp.Hp,
            Status       = bp.Status,
            SleepCounter = bp.SleepCounter,
            Base         = old.Base with { PP = (byte[])bp.PP.Clone() },
        };
    }

    // -------------------------------------------------------------------------
    // BattlePokemon construction
    // -------------------------------------------------------------------------

    private BattlePokemon BuildWildMon(string speciesId, int level)
    {
        var species = _data.Get<SpeciesData>(speciesId);
        var dvs     = new DVs(
            Attack:  (byte)Random.Shared.Next(16),
            Defense: (byte)Random.Shared.Next(16),
            Speed:   (byte)Random.Shared.Next(16),
            Special: (byte)Random.Shared.Next(16));
        var statExp = new StatExp(0, 0, 0, 0, 0);
        int maxHp   = _stats.CalcHp(species, dvs, statExp, level);
        var moves   = GetLevelMoves(species, level);
        return new BattlePokemon(
            SpeciesId:    speciesId,
            HeldItemId:   "NO_ITEM",
            Moves:        moves,
            DVs:          dvs,
            PP:           GetMovePP(moves),
            Happiness:    70,
            Level:        (byte)Math.Clamp(level, 1, 100),
            Status:       PrimaryStatus.None,
            SleepCounter: 0,
            Hp:           maxHp,
            MaxHp:        maxHp,
            Attack:       _stats.CalcStat(species, dvs, statExp, level, StatType.Attack),
            Defense:      _stats.CalcStat(species, dvs, statExp, level, StatType.Defense),
            Speed:        _stats.CalcStat(species, dvs, statExp, level, StatType.Speed),
            SpAtk:        _stats.CalcStat(species, dvs, statExp, level, StatType.SpAtk),
            SpDef:        _stats.CalcStat(species, dvs, statExp, level, StatType.SpDef),
            Type1Id:      species.Type1Id,
            Type2Id:      species.Type2Id);
    }

    private BattlePokemon BuildTrainerMon(string trainerId)
    {
        var trainer = _data.Get<TrainerData>(trainerId);
        var entry   = trainer.Party[0];
        var species = _data.Get<SpeciesData>(entry.SpeciesId);
        int level   = entry.Level;
        // Gen 2 default: trainer mons use DVs = 9 for all stats
        var dvs     = new DVs(9, 9, 9, 9);
        var statExp = new StatExp(0, 0, 0, 0, 0);
        int maxHp   = _stats.CalcHp(species, dvs, statExp, level);
        var moves   = PadTo4(entry.Moves ?? GetLevelMoves(species, level));
        return new BattlePokemon(
            SpeciesId:    entry.SpeciesId,
            HeldItemId:   entry.HeldItemId ?? "NO_ITEM",
            Moves:        moves,
            DVs:          dvs,
            PP:           GetMovePP(moves),
            Happiness:    70,
            Level:        entry.Level,
            Status:       PrimaryStatus.None,
            SleepCounter: 0,
            Hp:           maxHp,
            MaxHp:        maxHp,
            Attack:       _stats.CalcStat(species, dvs, statExp, level, StatType.Attack),
            Defense:      _stats.CalcStat(species, dvs, statExp, level, StatType.Defense),
            Speed:        _stats.CalcStat(species, dvs, statExp, level, StatType.Speed),
            SpAtk:        _stats.CalcStat(species, dvs, statExp, level, StatType.SpAtk),
            SpDef:        _stats.CalcStat(species, dvs, statExp, level, StatType.SpDef),
            Type1Id:      species.Type1Id,
            Type2Id:      species.Type2Id);
    }

    /// <summary>Returns the last ≤4 moves learnable at or below <paramref name="level"/>.</summary>
    private string[] GetLevelMoves(SpeciesData species, int level)
    {
        var learned = species.Learnset
            .Where(e => e.Level <= level)
            .Select(e => e.MoveId)
            .ToArray();
        int take   = Math.Min(4, learned.Length);
        var picked = learned.Skip(learned.Length - take).ToArray();
        return PadTo4(picked);
    }

    private static string[] PadTo4(string[] moves)
    {
        if (moves.Length >= 4) return moves[..4];
        var result = new string[4];
        Array.Fill(result, "NO_MOVE");
        Array.Copy(moves, result, moves.Length);
        return result;
    }

    private static string EventToText(BattleEvent ev) => ev switch
    {
        MoveUsedEvent        e => $"{(e.ByPlayer ? "You" : "Foe")} used {e.MoveName}!",
        MoveMissedEvent      e => $"{(e.ByPlayer ? "Your" : "Foe's")} attack missed!",
        DamageDealtEvent     e => $"{(e.ToPlayer ? "You" : "Foe")} took {e.Amount} dmg{(e.IsCritical ? " (crit!)" : "")}",
        StatusInflictedEvent e => $"{(e.ToPlayer ? "You" : "Foe")} became {e.Status}!",
        StatusCuredEvent     e => $"{(e.ToPlayer ? "You" : "Foe")} recovered!",
        FaintedEvent         e => $"{(e.IsPlayer ? "Your Pokemon" : "Foe Pokemon")} fainted!",
        EndOfTurnDamageEvent e => $"{(e.ToPlayer ? "You" : "Foe")} took {e.Amount} ({e.Source})",
        HealedEvent          e => $"{(e.ToPlayer ? "You" : "Foe")} healed {e.Amount} HP",
        FledEvent            _  => "Got away safely!",
        FleeFailed           _  => "Can't escape!",
        _                       => "...",
    };

    private static string StatusShort(PrimaryStatus s) => s switch
    {
        PrimaryStatus.Asleep    => "SLP",
        PrimaryStatus.Poisoned  => "PSN",
        PrimaryStatus.BadlyPoisoned => "TOX",
        PrimaryStatus.Burned    => "BRN",
        PrimaryStatus.Frozen    => "FRZ",
        PrimaryStatus.Paralyzed => "PAR",
        _                       => "",
    };

    private byte[] GetMovePP(string[] moveIds)
    {
        var pp = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            if (moveIds[i] != "NO_MOVE" &&
                _data.TryGet<MoveData>(moveIds[i], out var md) && md is not null)
                pp[i] = (byte)md.PP;
        }
        return pp;
    }
}
