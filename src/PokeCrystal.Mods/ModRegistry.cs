namespace PokeCrystal.Mods;

/// <summary>
/// Tracks all successfully loaded mods and their plugin instances.
/// Queried by the game shell for display, hot reload, and lifecycle events.
/// </summary>
public sealed class ModRegistry
{
    private readonly List<LoadedMod> _mods = new();

    public IReadOnlyList<LoadedMod> Loaded => _mods;

    internal void Add(LoadedMod mod) => _mods.Add(mod);

    public bool IsLoaded(string modId)
        => _mods.Any(m => m.Manifest.Id == modId);
}

public sealed class LoadedMod
{
    public ModManifest       Manifest { get; init; } = null!;
    public IModPlugin[]      Plugins  { get; init; } = [];
    public ModContext        Context  { get; init; } = null!;
}
