namespace PokeCrystal.Mods;

using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.Scripting;
using PokeCrystal.Scripting.Specials;

/// <summary>
/// Surface exposed to IModPlugin.Register(). Wraps the DI collection and
/// live registries so plugins can extend the engine without direct DI access.
/// </summary>
public sealed class ModContext
{
    private readonly IServiceCollection _services;
    private readonly IServiceProvider _provider;

    public ModContext(IServiceCollection services, IServiceProvider provider)
    {
        _services  = services;
        _provider  = provider;
        Manifest   = null!; // set by ModLoader before calling Register
    }

    /// <summary>The manifest of the mod currently being registered.</summary>
    public ModManifest Manifest { get; internal set; }

    // -----------------------------------------------------------------------
    // DI override — replace a core engine interface
    // -----------------------------------------------------------------------

    /// <summary>
    /// Replace a singleton engine interface with a mod-provided implementation.
    /// E.g. ctx.Override&lt;IDamageCalculator, MyDamageCalculator&gt;();
    /// </summary>
    public void Override<TInterface, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TInterface : class
        where TImpl : class, TInterface
        => _services.Add(new ServiceDescriptor(typeof(TInterface), typeof(TImpl), lifetime));

    /// <summary>Override with a pre-built instance.</summary>
    public void Override<TInterface>(TInterface instance) where TInterface : class
        => _services.AddSingleton(instance);

    // -----------------------------------------------------------------------
    // Script commands
    // -----------------------------------------------------------------------

    /// <summary>Register a new or replacement script command by opcode.</summary>
    public void RegisterCommand(IScriptCommand command)
        => _provider.GetRequiredService<ScriptEngine>().RegisterCommand(command);

    /// <summary>Register a special handler (dispatched by the Special command).</summary>
    public void RegisterSpecial(ISpecialHandler handler)
        => _provider.GetRequiredService<SpecialRegistry>().Register(handler);

    // -----------------------------------------------------------------------
    // Lifecycle hooks
    // -----------------------------------------------------------------------

    public event Action<string>? OnMapLoad;
    public event Action? OnBattleStart;
    public event Action? OnBattleEnd;
    public event Action? OnSave;
    public event Action? OnLoad;

    internal void FireMapLoad(string mapId)    => OnMapLoad?.Invoke(mapId);
    internal void FireBattleStart()            => OnBattleStart?.Invoke();
    internal void FireBattleEnd()              => OnBattleEnd?.Invoke();
    internal void FireSave()                   => OnSave?.Invoke();
    internal void FireLoad()                   => OnLoad?.Invoke();

    // -----------------------------------------------------------------------
    // Raw DI access (escape hatch for advanced plugins)
    // -----------------------------------------------------------------------

    public IServiceCollection Services  => _services;
    public IServiceProvider    Provider => _provider;
}
