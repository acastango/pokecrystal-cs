namespace PokeCrystal.Game;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Data;
using PokeCrystal.Game.Input;
using PokeCrystal.Game.Scenes;
using PokeCrystal.Rendering;
using PokeCrystal.World;

/// <summary>
/// Root MonoGame Game class. Bootstraps DI, wires all layers, and drives the
/// SceneManager each frame.
/// </summary>
public sealed class CrystalGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    private ServiceProvider _services = null!;
    private SceneManager _scenes = null!;
    private MonoGameInputProvider _input = null!;
    private IPaletteManager _palette = null!;
    private GameRenderer _renderer = null!;
    private TilesetCache _tilesetCache = null!;
    private DebugConsole _debug = null!;

    public CrystalGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth  = 480; // 160 × 3
        _graphics.PreferredBackBufferHeight = 432; // 144 × 3
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddCrystalGame();
        _services = serviceCollection.BuildServiceProvider();

        _input        = _services.GetRequiredService<MonoGameInputProvider>();
        _palette      = _services.GetRequiredService<IPaletteManager>();
        _scenes       = _services.GetRequiredService<SceneManager>();
        _renderer     = _services.GetRequiredService<GameRenderer>();
        _tilesetCache = _services.GetRequiredService<TilesetCache>();
        _debug        = _services.GetRequiredService<DebugConsole>();

        // Wire MapRegistry into WorldContext, then load map data from disk
        var ctx = _services.GetRequiredService<WorldContext>();
        ctx.Maps = _services.GetRequiredService<MapRegistry>();
        var mapsDir = Path.Combine(DataLoader.FindDataBase(), "maps");
        _services.GetRequiredService<MapLoader>().LoadAll(mapsDir);

        // Register debug commands
        _debug.Register("wild", _ =>
        {
            ctx.WildEncountersDisabled = !ctx.WildEncountersDisabled;
            return ctx.WildEncountersDisabled ? "Wild encounters OFF" : "Wild encounters ON";
        });

        // Feed typed characters into the debug console
        Window.TextInput += (_, e) => _debug.Feed(e.Character);

        // Reset debug log on every launch
        GameLog.Reset();

        // Start at the title screen
        _scenes.SetImmediate(_services.GetRequiredService<TitleScene>());

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Font — loaded from Content pipeline (Content/Font.spritefont → Font.xnb)
        try { _renderer.Font = Content.Load<SpriteFont>("Font"); }
        catch { /* content not built — draw calls will silently skip text */ }

        // 1×1 white pixel for filled rectangles and HP bars
        _renderer.Pixel = new Texture2D(GraphicsDevice, 1, 1);
        _renderer.Pixel.SetData([Color.White]);

        // Tileset graphics — needs GraphicsDevice to load PNG
        _tilesetCache.Initialize(GraphicsDevice);

        // Player sprite — needs GraphicsDevice to load PNG
        _services.GetRequiredService<PlayerSpriteRenderer>().Initialize(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        GameLog.Tick();
        _input.Update();
        _scenes.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _scenes.Draw(_spriteBatch);
        _renderer.DrawFade(_spriteBatch, _palette.FadeLevel,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _services?.Dispose();
        base.Dispose(disposing);
    }
}
