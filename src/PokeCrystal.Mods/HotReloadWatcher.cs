namespace PokeCrystal.Mods;

/// <summary>
/// Watches the data/ directory tree for JSON changes and triggers DataModMerger
/// on the changed file's mod directory. DLL plugins are excluded (require restart).
/// Intended for development use; disable in release builds.
/// </summary>
public sealed class HotReloadWatcher : IDisposable
{
    private readonly DataModMerger _merger;
    private readonly ModRegistry   _registry;
    private readonly List<FileSystemWatcher> _watchers = new();

    public HotReloadWatcher(DataModMerger merger, ModRegistry registry)
    {
        _merger   = merger;
        _registry = registry;
    }

    /// <summary>Start watching the given mod root directory (data/mods/).</summary>
    public void Watch(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory)) return;

        foreach (var mod in _registry.Loaded)
        {
            var dataDir = Path.Combine(mod.Manifest.ModDirectory, "data");
            if (!Directory.Exists(dataDir)) continue;

            var watcher = new FileSystemWatcher(dataDir, "*.json")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            watcher.Changed += (_, e) => OnFileChanged(e.FullPath, mod.Manifest);
            watcher.Created += (_, e) => OnFileChanged(e.FullPath, mod.Manifest);
            _watchers.Add(watcher);
        }
    }

    private void OnFileChanged(string filePath, ModManifest manifest)
    {
        // Debounce: the OS can fire multiple events per save
        try
        {
            var modDataDir = Path.Combine(manifest.ModDirectory, "data");
            _merger.Merge(modDataDir);
        }
        catch { /* log in future — don't crash the game on a bad hot-reload */ }
    }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }
}
