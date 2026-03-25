namespace PokeCrystal.Engine;

using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.Engine.AI;
using PokeCrystal.Engine.Battle;
using PokeCrystal.Engine.Pokemon;
using PokeCrystal.Schema;

/// <summary>
/// Registers all Gen 2 engine implementations into an IServiceCollection.
/// Call this after registering IDataRegistry (from PokeCrystal.Data).
///
/// Mods (L7) can override any calculator by re-registering before calling this,
/// or by replacing registrations after (last registration wins with TryAdd).
/// </summary>
public static class EngineRegistry
{
    public static IServiceCollection AddCrystalEngine(this IServiceCollection services)
    {
        services.AddSingleton<ITypeEffectivenessResolver, TypeEffectivenessResolver>();
        services.AddSingleton<IDamageCalculator, DamageCalculator>();
        services.AddSingleton<IStatCalculator, StatCalculator>();
        services.AddSingleton<ICatchCalculator, CatchCalculator>();
        services.AddSingleton<IExperienceCalculator, ExperienceCalculator>();
        services.AddSingleton<IEvolutionEvaluator, EvolutionEvaluator>();
        services.AddSingleton<IBreedingCalculator, BreedingCalculator>();

        // AI strategies — keyed collection resolved by strategy key
        services.AddSingleton<IAIStrategy, BasicAI>();
        services.AddSingleton<IAIStrategy, SmartAI>();

        // Battle turn loop — depends on calculators + AI + data registry
        services.AddSingleton<BattleEngine>();

        return services;
    }
}
