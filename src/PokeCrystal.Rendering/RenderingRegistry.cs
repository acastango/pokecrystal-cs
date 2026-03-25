namespace PokeCrystal.Rendering;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for all L5 Rendering services.
/// Call services.AddCrystalRendering() from the game layer host (L6).
/// </summary>
public static class RenderingRegistry
{
    public static IServiceCollection AddCrystalRendering(this IServiceCollection services)
    {
        services.AddSingleton<IPaletteManager, PaletteManager>();
        services.AddSingleton<IAudioRenderer, MonoGameAudioRenderer>();
        return services;
    }
}
