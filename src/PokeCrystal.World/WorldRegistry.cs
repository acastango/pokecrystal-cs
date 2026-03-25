namespace PokeCrystal.World;

using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.World.Systems;

/// <summary>
/// DI registration for all L4 World services.
/// Call services.AddCrystalWorld() from the game layer host.
/// </summary>
public static class WorldRegistry
{
    public static IServiceCollection AddCrystalWorld(this IServiceCollection services)
    {
        services.AddSingleton<MapRegistry>();
        services.AddSingleton<MapLoader>();

        // Systems — registered in execution order
        services.AddSingleton<TimeSystem>();
        services.AddSingleton<PlayerController>();
        services.AddSingleton<WarpSystem>();
        services.AddSingleton<CoordEventSystem>();
        services.AddSingleton<WildEncounterSystem>();
        services.AddSingleton<MapObjectSystem>();

        // Register all as IWorldSystem in order
        services.AddSingleton<IWorldSystem>(sp => sp.GetRequiredService<TimeSystem>());
        services.AddSingleton<IWorldSystem>(sp => sp.GetRequiredService<PlayerController>());
        services.AddSingleton<IWorldSystem>(sp => sp.GetRequiredService<WarpSystem>());
        services.AddSingleton<IWorldSystem>(sp => sp.GetRequiredService<CoordEventSystem>());
        services.AddSingleton<IWorldSystem>(sp => sp.GetRequiredService<WildEncounterSystem>());
        services.AddSingleton<IWorldSystem>(sp => sp.GetRequiredService<MapObjectSystem>());

        services.AddSingleton<OverworldEngine>();

        return services;
    }
}
