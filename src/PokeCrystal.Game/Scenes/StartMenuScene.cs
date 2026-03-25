namespace PokeCrystal.Game.Scenes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Schema;
using PokeCrystal.World;
using XnaGameTime = Microsoft.Xna.Framework.GameTime;

/// <summary>
/// Start menu (B/Start button in overworld) — L14.
/// Items: POKéMON → PartyScene, BAG → stub, SAVE → JSON save, EXIT → overworld.
/// </summary>
public sealed class StartMenuScene : IScene
{
    private static readonly string[] Items = ["POKéMON", "BAG", "SAVE", "EXIT"];

    private const int BoxX   = 296;
    private const int BoxY   =  20;
    private const int BoxW   = 172;
    private const int ItemH  =  30;
    private const int PadX   =  12;
    private const int PadY   =  10;

    private readonly SceneManager    _scenes;
    private readonly WorldContext    _ctx;
    private readonly SaveSystem      _save;
    private readonly IInputProvider  _input;
    private readonly PartyScene      _party;
    private readonly IServiceProvider _sp;
    private readonly GameRenderer    _renderer;

    private int _cursor;

    public StartMenuScene(
        SceneManager     scenes,
        WorldContext     ctx,
        SaveSystem       save,
        IInputProvider   input,
        PartyScene       party,
        IServiceProvider sp,
        GameRenderer     renderer)
    {
        _scenes   = scenes;
        _ctx      = ctx;
        _save     = save;
        _input    = input;
        _party    = party;
        _sp       = sp;
        _renderer = renderer;
    }

    // -------------------------------------------------------------------------
    // IScene
    // -------------------------------------------------------------------------

    public void OnEnter() => _cursor = 0;
    public void OnExit()  { }

    public void Update(XnaGameTime gameTime)
    {
        if (_input.IsPressed(GameAction.MoveUp))
            _cursor = (_cursor - 1 + Items.Length) % Items.Length;
        else if (_input.IsPressed(GameAction.MoveDown))
            _cursor = (_cursor + 1) % Items.Length;

        if (_input.IsPressed(GameAction.Confirm))
            Activate();
        else if (_input.IsPressed(GameAction.Cancel) || _input.IsPressed(GameAction.Menu))
            ReturnToOverworld();
    }

    public void Draw(SpriteBatch sb)
    {
        int boxH = Items.Length * ItemH + PadY * 2;

        // Panel background
        _renderer.FillRect(sb, BoxX - 4, BoxY - 4, BoxW + 8, boxH + 8, GameRenderer.Divider);
        _renderer.FillRect(sb, BoxX, BoxY, BoxW, boxH, GameRenderer.BgPanel);

        // Items
        for (int i = 0; i < Items.Length; i++)
        {
            int y      = BoxY + PadY + i * ItemH;
            bool sel   = i == _cursor;
            var color  = sel ? GameRenderer.TextMain : GameRenderer.TextDim;
            string pre = sel ? "> " : "  ";
            _renderer.DrawText(sb, pre + Items[i], BoxX + PadX, y, color);
        }
    }

    // -------------------------------------------------------------------------
    // Menu actions
    // -------------------------------------------------------------------------

    private void Activate()
    {
        switch (Items[_cursor])
        {
            case "POKéMON":
                _scenes.Transition(_party);
                break;

            case "BAG":
                ReturnToOverworld();
                break;

            case "SAVE":
                DoSave();
                ReturnToOverworld();
                break;

            case "EXIT":
                ReturnToOverworld();
                break;
        }
    }

    private void ReturnToOverworld()
        => _scenes.Transition(_sp.GetRequiredService<OverworldScene>());

    private void DoSave()
    {
        var saveFile = new SaveFile(
            Options:     new GameOptions(
                             TextSpeed.Fast, BattleAnimations: true,
                             BattleStyle: BattleStyleMode.Shift,
                             Sound: SoundMode.Stereo,
                             MenuAccount: false, FrameStyle: false),
            PlayerData:  _ctx.Player,
            MapPosition: new MapPosition(
                             _ctx.CurrentMapId, _ctx.PlayerX, _ctx.PlayerY, _ctx.Facing),
            PokemonData: _ctx.Pokemon,
            PcBoxes:     [],
            ActiveBox:   new PcBox("BOX1", []),
            HallOfFame:  [],
            LinkStats:   new LinkBattleStats(0, 0, 0, []),
            Mail:        new MailData([], []),
            CrystalData: new CrystalData(HasGsBall: false, GsBallDelivered: false));

        _save.Save(saveFile);
    }
}
