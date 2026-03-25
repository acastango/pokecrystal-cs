namespace PokeCrystal.Game;

using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.Data;
using PokeCrystal.Engine;
using PokeCrystal.Game.Input;
using PokeCrystal.Game.Scenes;
using PokeCrystal.Rendering;
using PokeCrystal.Schema;
using PokeCrystal.Scripting;
using PokeCrystal.World;

/// <summary>
/// DI wiring for L6 and all lower layers.
/// Call services.AddCrystalGame() from CrystalGame.Initialize().
/// </summary>
public static class GameRegistry
{
    public static IServiceCollection AddCrystalGame(this IServiceCollection services)
    {
        // Data registry — must be registered before engine (calculators depend on it)
        services.AddSingleton<IDataRegistry>(_ => DataLoader.LoadAll());

        // Lower layers
        services.AddCrystalEngine();
        services.AddCrystalScripting();
        services.AddCrystalWorld();
        services.AddCrystalRendering();

        // Input
        services.AddSingleton<MonoGameInputProvider>();
        services.AddSingleton<IInputProvider>(sp => sp.GetRequiredService<MonoGameInputProvider>());

        // Shared renderer (Font + Pixel set in CrystalGame.LoadContent)
        services.AddSingleton<GameRenderer>();

        // Debug console
        services.AddSingleton<DebugConsole>();

        // Tileset graphics cache (initialized in CrystalGame.LoadContent)
        services.AddSingleton<TilesetCache>(_ =>
        {
            var dataBase = PokeCrystal.Data.DataLoader.FindDataBase();
            var tilesetsDir = Path.Combine(dataBase, "..", "tilesets");
            return new TilesetCache(tilesetsDir);
        });

        // Player sprite renderer (initialized in CrystalGame.LoadContent)
        services.AddSingleton<PlayerSpriteRenderer>(_ =>
        {
            var dataBase = PokeCrystal.Data.DataLoader.FindDataBase();
            var spritesDir = Path.Combine(dataBase, "..", "sprites");
            return new PlayerSpriteRenderer(spritesDir);
        });

        // Save system
        services.AddSingleton<SaveSystem>();

        // World state (one instance shared across scenes)
        services.AddSingleton<WorldContext>();

        // Scenes (registered as singletons so cross-references resolve cleanly)
        services.AddSingleton<SceneManager>();
        services.AddSingleton<OverworldScene>();
        services.AddSingleton<BattleScene>();
        services.AddSingleton<TitleScene>();
        services.AddSingleton<StartMenuScene>();
        services.AddSingleton<PartyScene>();

        return services;
    }
}
