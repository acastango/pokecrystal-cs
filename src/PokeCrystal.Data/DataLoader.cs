using System.Text.Json;
using System.Text.Json.Serialization;
using PokeCrystal.Schema;

namespace PokeCrystal.Data;

/// <summary>
/// Reads JSON data files from data/ and populates an IDataRegistry.
/// Load order: species → moves → items → types/matchups → trainers.
/// Maps are loaded by L4 (World Engine) which also handles map scripting.
/// </summary>
public static class DataLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Load all base-game data from dataRoot (defaults to "data" relative to executable).
    /// Returns a fully populated, ready-to-use registry.
    /// </summary>
    public static IDataRegistry LoadAll(string? dataRoot = null)
    {
        var root = ResolveRoot(dataRoot);
        var registry = new DataRegistry();

        LoadDirectory<SpeciesData>(registry, root, "pokemon");
        LoadFile<MoveData>(registry, root, "moves.json");
        LoadFile<ItemData>(registry, root, "items.json");
        LoadFile<TypeMatchup>(registry, root, "type_matchups.json");
        LoadDirectory<TrainerData>(registry, root, "trainers");

        return registry;
    }

    /// <summary>
    /// Merge a mod overlay into an existing registry.
    /// Mod entries override base-game entries with the same ID.
    /// </summary>
    public static void LoadMod(string modRoot, IDataRegistry registry)
    {
        var root = new DirectoryInfo(modRoot);
        if (!root.Exists)
            throw new DirectoryNotFoundException($"Mod root not found: {modRoot}");

        MergeDirectory<SpeciesData>(registry, root, "pokemon");
        MergeFile<MoveData>(registry, root, "moves.json");
        MergeFile<ItemData>(registry, root, "items.json");
        MergeFile<TypeMatchup>(registry, root, "type_matchups.json");
        MergeDirectory<TrainerData>(registry, root, "trainers");
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// Locates the data/base/ directory by walking up from the executable.
    /// Finds it whether running from bin/Debug/net9.0/ or the repo root.
    /// </summary>
    public static string FindDataBase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "base");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Cannot find data/base/. Ensure the repo data/ directory is present.");
    }

    private static DirectoryInfo ResolveRoot(string? dataRoot)
    {
        var path = dataRoot ?? FindDataBase();
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Data root not found: {path}");
        return dir;
    }

    private static void LoadDirectory<T>(IDataRegistry registry, DirectoryInfo root, string subdir)
        where T : IIdentifiable
    {
        var dir = new DirectoryInfo(Path.Combine(root.FullName, subdir));
        if (!dir.Exists) return;
        foreach (var file in dir.EnumerateFiles("*.json"))
            foreach (var item in DeserializeArray<T>(file))
                registry.Register(item);
    }

    private static void LoadFile<T>(IDataRegistry registry, DirectoryInfo root, string filename)
        where T : IIdentifiable
    {
        var file = new FileInfo(Path.Combine(root.FullName, filename));
        if (!file.Exists) return;
        foreach (var item in DeserializeArray<T>(file))
            registry.Register(item);
    }

    private static void MergeDirectory<T>(IDataRegistry registry, DirectoryInfo root, string subdir)
        where T : IIdentifiable => LoadDirectory<T>(registry, root, subdir);

    private static void MergeFile<T>(IDataRegistry registry, DirectoryInfo root, string filename)
        where T : IIdentifiable => LoadFile<T>(registry, root, filename);

    private static IEnumerable<T> DeserializeArray<T>(FileInfo file)
    {
        using var stream = file.OpenRead();
        // Files may contain a single object or an array
        var doc = JsonDocument.Parse(stream);
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<T[]>(doc.RootElement.GetRawText(), _opts) ?? []
            : [JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), _opts)!];
    }
}
