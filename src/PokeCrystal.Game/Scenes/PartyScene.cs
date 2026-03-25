namespace PokeCrystal.Game.Scenes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Schema;
using PokeCrystal.World;
using XnaGameTime = Microsoft.Xna.Framework.GameTime;

/// <summary>
/// Party screen — L14. Shows the player's active party (up to 6) with
/// species, level, current HP / max HP, and status.
///
/// Navigation: Up/Down move the cursor. Cancel returns to StartMenuScene.
/// Confirm is a stub (summary screen would be wired in a future pass).
/// </summary>
public sealed class PartyScene : IScene
{
    private const int HeaderH  =  40;
    private const int RowH     =  56;
    private const int BarW     = 120;
    private const int BarH     =  12;
    private const int ScreenW  = 480;

    private readonly SceneManager     _scenes;
    private readonly WorldContext     _ctx;
    private readonly IInputProvider   _input;
    private readonly IServiceProvider _sp;
    private readonly GameRenderer     _renderer;

    private int _cursor;

    public PartyScene(
        SceneManager     scenes,
        WorldContext     ctx,
        IInputProvider   input,
        IServiceProvider sp,
        GameRenderer     renderer)
    {
        _scenes   = scenes;
        _ctx      = ctx;
        _input    = input;
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
        var party = _ctx.Pokemon.Party;
        int count = party.Count;
        if (count == 0)
        {
            if (_input.IsPressed(GameAction.Cancel) || _input.IsPressed(GameAction.Menu))
                ReturnToStartMenu();
            return;
        }

        if (_input.IsPressed(GameAction.MoveUp))
            _cursor = (_cursor - 1 + count) % count;
        else if (_input.IsPressed(GameAction.MoveDown))
            _cursor = (_cursor + 1) % count;

        if (_input.IsPressed(GameAction.Cancel) || _input.IsPressed(GameAction.Menu))
            ReturnToStartMenu();
    }

    public void Draw(SpriteBatch sb)
    {
        _renderer.FillRect(sb, 0, 0, ScreenW, 432, GameRenderer.BgDark);

        // Header
        _renderer.FillRect(sb, 0, 0, ScreenW, HeaderH, GameRenderer.BgPanel);
        _renderer.DrawText(sb, "POKEMON", 20, 10, GameRenderer.Cursor);
        _renderer.DrawText(sb, "B/X: back", ScreenW - 120, 10, GameRenderer.TextDim);

        var party = _ctx.Pokemon?.Party;
        if (party is null || party.Count == 0)
        {
            _renderer.DrawText(sb, "(no Pokemon)", 20, HeaderH + 20, GameRenderer.TextDim);
            return;
        }

        for (int i = 0; i < party.Count; i++)
        {
            var mon  = party[i];
            int rowY = HeaderH + i * RowH;
            bool sel = i == _cursor;

            // Row background
            var rowBg = sel ? GameRenderer.BgPanel : GameRenderer.BgDark;
            _renderer.FillRect(sb, 0, rowY, ScreenW, RowH - 2, rowBg);
            if (sel)
                _renderer.FillRect(sb, 0, rowY, 4, RowH - 2, GameRenderer.Cursor);

            // Name + level
            string name    = mon.Base.SpeciesId;
            string lvText  = $"Lv.{mon.Base.Level}";
            var    nameCol = sel ? GameRenderer.TextMain : GameRenderer.TextDim;
            _renderer.DrawText(sb, name,   20,  rowY + 10, nameCol);
            _renderer.DrawText(sb, lvText, 200, rowY + 10, GameRenderer.TextDim);

            // Status tag
            if (mon.Status != PrimaryStatus.None)
                _renderer.DrawText(sb, $"[{StatusShort(mon.Status)}]", 270, rowY + 10, GameRenderer.HpLow);

            // HP bar
            _renderer.DrawHpBar(sb, 310, rowY + 14, BarW, BarH, mon.CurrentHp, mon.MaxHp);

            // HP numbers
            string hpText = $"{mon.CurrentHp}/{mon.MaxHp}";
            _renderer.DrawText(sb, hpText, 440, rowY + 10, GameRenderer.TextDim);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ReturnToStartMenu()
        => _scenes.Transition(_sp.GetRequiredService<StartMenuScene>());

    private static string StatusShort(PrimaryStatus s) => s switch
    {
        PrimaryStatus.Asleep        => "SLP",
        PrimaryStatus.Poisoned      => "PSN",
        PrimaryStatus.BadlyPoisoned => "TOX",
        PrimaryStatus.Burned        => "BRN",
        PrimaryStatus.Frozen        => "FRZ",
        PrimaryStatus.Paralyzed     => "PAR",
        _                           => "",
    };
}
