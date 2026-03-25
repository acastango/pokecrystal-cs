namespace PokeCrystal.Mods;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Discovers, sorts, and loads all mods from the mods directory.
///
/// Loading pipeline per mod:
///   1. Parse manifest.json
///   2. Topological sort by Dependencies + Priority
///   3. Merge data files (DataModMerger)
///   4. Load DLL plugins → call IModPlugin.Register(ModContext)
/// </summary>
public sealed class ModLoader
{
    private static readonly JsonSerializerOptions ManifestOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DataModMerger     _merger;
    private readonly ModRegistry       _registry;
    private readonly IServiceCollection _services;
    private readonly IServiceProvider  _provider;

    public ModLoader(
        DataModMerger     merger,
        ModRegistry       registry,
        IServiceCollection services,
        IServiceProvider  provider)
    {
        _merger   = merger;
        _registry = registry;
        _services = services;
        _provider = provider;
    }

    /// <summary>
    /// Load all mods from modsDirectory (typically data/mods/).
    /// Call once at startup after base data is loaded.
    /// </summary>
    public void LoadAll(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory)) return;

        var manifests = DiscoverManifests(modsDirectory);
        var sorted    = TopologicalSort(manifests);

        foreach (var manifest in sorted)
            LoadMod(manifest);
    }

    // -----------------------------------------------------------------------
    // Discovery
    // -----------------------------------------------------------------------

    private static List<ModManifest> DiscoverManifests(string modsDirectory)
    {
        var manifests = new List<ModManifest>();
        foreach (var dir in Directory.EnumerateDirectories(modsDirectory))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var m = JsonSerializer.Deserialize<ModManifest>(json, ManifestOpts);
                if (m is null || string.IsNullOrEmpty(m.Id)) continue;
                m.ModDirectory = dir;
                manifests.Add(m);
            }
            catch { /* skip malformed manifests */ }
        }
        return manifests;
    }

    // -----------------------------------------------------------------------
    // Topological sort (Kahn's algorithm)
    // -----------------------------------------------------------------------

    private static List<ModManifest> TopologicalSort(List<ModManifest> manifests)
    {
        var byId = manifests.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var inDegree = manifests.ToDictionary(m => m.Id, _ => 0, StringComparer.Ordinal);

        foreach (var m in manifests)
            foreach (var dep in m.Dependencies)
                if (byId.ContainsKey(dep))
                    inDegree[m.Id]++;

        // Seed with nodes that have no unresolved dependencies, ordered by priority
        var ready = new SortedSet<(int Priority, string Id)>(
            manifests.Where(m => inDegree[m.Id] == 0)
                     .Select(m => (-m.Priority, m.Id))); // negate: lower value = higher priority → loaded last

        var sorted = new List<ModManifest>();
        while (ready.Count > 0)
        {
            var (_, id) = ready.Min;
            ready.Remove(ready.Min);
            sorted.Add(byId[id]);

            foreach (var m in manifests)
            {
                if (!m.Dependencies.Contains(id, StringComparer.Ordinal)) continue;
                inDegree[m.Id]--;
                if (inDegree[m.Id] == 0)
                    ready.Add((-m.Priority, m.Id));
            }
        }

        // Append any remaining (circular deps — load them anyway)
        sorted.AddRange(manifests.Where(m => !sorted.Contains(m)));
        return sorted;
    }

    // -----------------------------------------------------------------------
    // Per-mod loading
    // -----------------------------------------------------------------------

    private void LoadMod(ModManifest manifest)
    {
        // 1. Merge data files
        var dataDir = Path.Combine(manifest.ModDirectory, "data");
        _merger.Merge(dataDir);

        // 2. Load DLL plugins
        var plugins = LoadPlugins(manifest);

        var ctx = new ModContext(_services, _provider) { Manifest = manifest };

        foreach (var plugin in plugins)
        {
            try { plugin.Register(ctx); }
            catch { /* log in future; don't abort loading other mods */ }
        }

        _registry.Add(new LoadedMod
        {
            Manifest = manifest,
            Plugins  = plugins,
            Context  = ctx,
        });
    }

    private static IModPlugin[] LoadPlugins(ModManifest manifest)
    {
        var pluginsDir = Path.Combine(manifest.ModDirectory, "plugins");
        if (!Directory.Exists(pluginsDir)) return [];

        var plugins = new List<IModPlugin>();
        foreach (var dll in Directory.EnumerateFiles(pluginsDir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetExportedTypes())
                {
                    if (!type.IsClass || type.IsAbstract) continue;
                    if (!typeof(IModPlugin).IsAssignableFrom(type)) continue;

                    if (Activator.CreateInstance(type) is IModPlugin plugin)
                        plugins.Add(plugin);
                }
            }
            catch { /* skip unloadable assemblies */ }
        }
        return [.. plugins];
    }
}
