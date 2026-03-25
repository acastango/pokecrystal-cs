namespace PokeCrystal.Mods;

using System.Text.Json;
using System.Text.Json.Serialization;
using PokeCrystal.Data;
using PokeCrystal.Schema;
using PokeCrystal.World;

/// <summary>
/// Merges a mod's data files into the live registries.
/// Mirrors the structure of base/data/ — any JSON file present in the mod
/// overlays the corresponding base entry by its "id" field.
/// Sub-directories determine the target registry:
///   species/   → DataRegistry&lt;SpeciesData&gt;
///   moves/     → DataRegistry&lt;MoveData&gt;
///   items/     → DataRegistry&lt;ItemData&gt;
///   maps/      → MapRegistry
///   music/     → AudioRegistry (music)
///   sfx/       → AudioRegistry (sfx)
/// </summary>
public sealed class DataModMerger
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDataRegistry  _data;
    private readonly MapRegistry    _maps;
    private readonly AudioRegistry  _audio;
    private readonly MapLoader      _mapLoader;

    public DataModMerger(
        IDataRegistry data,
        MapRegistry   maps,
        AudioRegistry audio,
        MapLoader     mapLoader)
    {
        _data      = data;
        _maps      = maps;
        _audio     = audio;
        _mapLoader = mapLoader;
    }

    /// <summary>
    /// Merge all JSON files from modDataDir into the live registries.
    /// modDataDir is expected to contain sub-folders mirroring base/data/.
    /// </summary>
    public void Merge(string modDataDir)
    {
        if (!Directory.Exists(modDataDir)) return;

        MergeTyped<SpeciesData>(Path.Combine(modDataDir, "species"));
        MergeTyped<MoveData>(Path.Combine(modDataDir, "moves"));
        MergeTyped<ItemData>(Path.Combine(modDataDir, "items"));
        MergeTyped<TrainerData>(Path.Combine(modDataDir, "trainers"));
        MergeMaps(Path.Combine(modDataDir, "maps"));
        MergeAudio(Path.Combine(modDataDir, "music"), isMusic: true);
        MergeAudio(Path.Combine(modDataDir, "sfx"),   isMusic: false);
    }

    private void MergeTyped<T>(string dir) where T : IIdentifiable
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var item = JsonSerializer.Deserialize<T>(json, JsonOpts);
                if (item is not null) _data.Register(item);
            }
            catch { /* skip malformed mod files */ }
        }
    }

    private void MergeMaps(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try { _mapLoader.LoadFile(file); }
            catch { }
        }
    }

    private void MergeAudio(string dir, bool isMusic)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                if (isMusic)
                {
                    var m = JsonSerializer.Deserialize<MusicData>(json, JsonOpts);
                    if (m is not null) _audio.RegisterMusic(m);
                }
                else
                {
                    var s = JsonSerializer.Deserialize<SfxData>(json, JsonOpts);
                    if (s is not null) _audio.RegisterSfx(s);
                }
            }
            catch { }
        }
    }
}
