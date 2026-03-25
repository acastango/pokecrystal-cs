namespace PokeCrystal.Mods;

/// <summary>
/// Deserialized from data/mods/{id}/manifest.json.
/// Declares identity, dependencies, and load priority.
/// </summary>
public sealed class ModManifest
{
    /// <summary>Unique machine-readable key, e.g. "my_mod" or "author.my_mod".</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>IDs of mods that must be loaded before this one.</summary>
    public string[] Dependencies { get; set; } = [];

    /// <summary>Higher priority loads later and therefore wins data conflicts.</summary>
    public int Priority { get; set; } = 0;

    /// <summary>Minimum game version required (semver string, optional).</summary>
    public string GameVersionMin { get; set; } = string.Empty;

    /// <summary>Directory path this manifest was loaded from.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ModDirectory { get; set; } = string.Empty;
}
