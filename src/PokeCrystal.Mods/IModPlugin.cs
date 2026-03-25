namespace PokeCrystal.Mods;

/// <summary>
/// Entry point for compiled C# mod plugins (DLLs).
/// Implement this interface in your mod assembly; the loader discovers and
/// calls Register() once during startup.
/// </summary>
public interface IModPlugin
{
    /// <summary>Must match the manifest Id of the mod that ships this DLL.</summary>
    string ModId { get; }

    /// <summary>
    /// Called after data files are merged and base services are registered.
    /// Use ModContext to register new engine implementations, script commands,
    /// lifecycle hooks, and service overrides.
    /// </summary>
    void Register(ModContext ctx);
}
