namespace PokeCrystal.Mods;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for L7 Mod Runtime services.
/// Call services.AddCrystalMods() from the game layer after AddCrystalGame().
/// ModLoader.LoadAll() must be called explicitly after the container is built.
/// </summary>
public static class ModsRegistry
{
    public static IServiceCollection AddCrystalMods(this IServiceCollection services)
    {
        services.AddSingleton<ModRegistry>();
        services.AddSingleton<DataModMerger>();
        services.AddSingleton<HotReloadWatcher>();

        // ModLoader needs IServiceCollection + IServiceProvider — provided at build time
        services.AddSingleton<ModLoader>(sp => new ModLoader(
            sp.GetRequiredService<DataModMerger>(),
            sp.GetRequiredService<ModRegistry>(),
            services,   // the same collection used to build this container
            sp));

        return services;
    }
}
