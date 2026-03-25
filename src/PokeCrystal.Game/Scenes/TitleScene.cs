namespace PokeCrystal.Game.Scenes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Data;
using PokeCrystal.Schema;
using PokeCrystal.World;
using XnaGameTime = Microsoft.Xna.Framework.GameTime;

/// <summary>
/// Title screen — L15. Shows CONTINUE (if save slot 0 exists) and NEW GAME.
///
/// CONTINUE: loads SaveFile slot 0, populates WorldContext, transitions to OverworldScene.
/// NEW GAME: builds a fresh Cyndaquil L5 party, default PlayerData,
///           places player on ROUTE_29, transitions to OverworldScene.
/// </summary>
public sealed class TitleScene : IScene
{
    private const int ScreenW = 480;
    private const int ScreenH = 432;

    private readonly SceneManager    _scenes;
    private readonly WorldContext    _ctx;
    private readonly SaveSystem      _save;
    private readonly IDataRegistry   _data;
    private readonly IStatCalculator _stats;
    private readonly IInputProvider  _input;
    private readonly IServiceProvider _sp;
    private readonly GameRenderer    _renderer;

    private string[] _items = [];
    private int _cursor;

    public TitleScene(
        SceneManager     scenes,
        WorldContext     ctx,
        SaveSystem       save,
        IDataRegistry    data,
        IStatCalculator  stats,
        IInputProvider   input,
        IServiceProvider sp,
        GameRenderer     renderer)
    {
        _scenes   = scenes;
        _ctx      = ctx;
        _save     = save;
        _data     = data;
        _stats    = stats;
        _input    = input;
        _sp       = sp;
        _renderer = renderer;
    }

    // -------------------------------------------------------------------------
    // IScene
    // -------------------------------------------------------------------------

    public void OnEnter()
    {
        _cursor = 0;
        _items  = _save.SlotExists(0)
            ? ["CONTINUE", "NEW GAME"]
            : ["NEW GAME"];
    }

    public void OnExit() { }

    public void Update(XnaGameTime gameTime)
    {
        if (_input.IsPressed(GameAction.MoveUp))
            _cursor = (_cursor - 1 + _items.Length) % _items.Length;
        else if (_input.IsPressed(GameAction.MoveDown))
            _cursor = (_cursor + 1) % _items.Length;

        if (_input.IsPressed(GameAction.Confirm))
            Activate();
    }

    public void Draw(SpriteBatch sb)
    {
        // Background
        _renderer.FillRect(sb, 0, 0, ScreenW, ScreenH, GameRenderer.BgDark);

        // Title
        _renderer.DrawTextCentered(sb, "POKEMON CRYSTAL CS", 0, 80, ScreenW, GameRenderer.Cursor);
        _renderer.DrawTextCentered(sb, "- - - - - - - - -", 0, 108, ScreenW, GameRenderer.Divider);

        // Menu items — centered block
        const int itemH  = 32;
        int menuTop = 200;
        for (int i = 0; i < _items.Length; i++)
        {
            int y     = menuTop + i * itemH;
            var color = i == _cursor ? GameRenderer.TextMain : GameRenderer.TextDim;

            if (i == _cursor)
                _renderer.DrawTextCentered(sb, "> " + _items[i], 0, y, ScreenW, GameRenderer.Cursor);
            else
                _renderer.DrawTextCentered(sb, "  " + _items[i], 0, y, ScreenW, color);
        }

        // Footer
        _renderer.DrawTextCentered(sb, "Z / ENTER to select", 0, ScreenH - 36, ScreenW, GameRenderer.TextDim);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void Activate()
    {
        switch (_items[_cursor])
        {
            case "CONTINUE":
                var sf = _save.Load(0);
                if (sf is not null) LoadSave(sf);
                break;

            case "NEW GAME":
                StartNewGame();
                break;
        }
        _scenes.Transition(_sp.GetRequiredService<OverworldScene>());
    }

    private void LoadSave(SaveFile sf)
    {
        _ctx.Player         = sf.PlayerData;
        _ctx.Pokemon        = sf.PokemonData;
        _ctx.CurrentMapId   = sf.MapPosition.MapId;
        _ctx.PlayerX        = sf.MapPosition.X;
        _ctx.PlayerY        = sf.MapPosition.Y;
        _ctx.Facing         = sf.MapPosition.Facing;
    }

    private void StartNewGame()
    {
        var species = _data.Get<SpeciesData>("CYNDAQUIL");
        var dvs     = new DVs(10, 10, 10, 10);
        var statExp = new StatExp(0, 0, 0, 0, 0);
        const byte level = 5;

        var stored = new StoredPokemon(
            SpeciesId:        "CYNDAQUIL",
            HeldItemId:       "NO_ITEM",
            Moves:            ["TACKLE", "LEER", "NO_MOVE", "NO_MOVE"],
            TrainerId:        0,
            Exp:              0,
            StatExp:          statExp,
            DVs:              dvs,
            PP:               [35, 30, 0, 0],
            Happiness:        70,
            PokerusStatus:    0,
            CaughtTimeOfDay:  TimeOfDay.Day,
            CaughtLevel:      level,
            CaughtGender:     Gender.Male,
            CaughtLocationId: "NEW_BARK_TOWN",
            Level:            level);

        int maxHp = _stats.CalcHp(species, dvs, statExp, level);
        var mon = new PartyPokemon(
            Base:         stored,
            Status:       PrimaryStatus.None,
            SleepCounter: 0,
            CurrentHp:    maxHp,
            MaxHp:        maxHp,
            Attack:       _stats.CalcStat(species, dvs, statExp, level, StatType.Attack),
            Defense:      _stats.CalcStat(species, dvs, statExp, level, StatType.Defense),
            Speed:        _stats.CalcStat(species, dvs, statExp, level, StatType.Speed),
            SpAtk:        _stats.CalcStat(species, dvs, statExp, level, StatType.SpAtk),
            SpDef:        _stats.CalcStat(species, dvs, statExp, level, StatType.SpDef));

        _ctx.Player = new PlayerData(
            TrainerId:        (ushort)Random.Shared.Next(65536),
            SecretId:         (ushort)Random.Shared.Next(65536),
            PlayerName:       "GOLD",
            RivalName:        "SILVER",
            PlayerGender:     Gender.Male,
            Money:            3000,
            Coins:            0,
            JohtoBadges:      new BadgeSet(new bool[8]),
            KantoBadges:      new BadgeSet(new bool[8]),
            TmsHMs:           new TmHmSet(new bool[57]),
            Items:            new BagPocket([], 20),
            KeyItems:         new BagPocket([], 26),
            Balls:            new BagPocket([], 12),
            PcItems:          new BagPocket([], 50),
            PokegearFlags:    PokegearFlags.None,
            PhoneContacts:    [],
            StatusFlags:      PlayerStatusFlags.None,
            Mom:              new MomData(false, 0),
            GameTime:         new PokeCrystal.Schema.GameTime(0, 0, 0),
            CurrentTimeOfDay: TimeOfDay.Day);

        _ctx.Pokemon = new PokemonData(
            Party:         [mon],
            PokedexCaught: new EventFlagSet(),
            PokedexSeen:   new EventFlagSet(),
            UnownDex:      new bool[28],
            DayCareSlot1:  null,
            DayCareSlot2:  null,
            StepsToEgg:    0,
            Roamers:       []);

        _ctx.CurrentMapId = "ROUTE_29";
        _ctx.PlayerX      = 5;
        _ctx.PlayerY      = 5;
        _ctx.Facing       = FacingDirection.Down;
    }
}
