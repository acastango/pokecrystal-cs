namespace PokeCrystal.Game;

using System.Text.Json;
using System.Text.Json.Serialization;
using PokeCrystal.Schema;

/// <summary>
/// JSON-based save system. Each slot is a separate file in saves/{slot}.json.
/// Unknown fields from newer mod versions are preserved on round-trip via
/// JsonSerializer's default behavior.
/// </summary>
public sealed class SaveSystem
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _saveDirectory;

    public SaveSystem(string saveDirectory = "saves")
        => _saveDirectory = saveDirectory;

    public void Save(SaveFile saveFile, int slot = 0)
    {
        Directory.CreateDirectory(_saveDirectory);
        var path = SlotPath(slot);
        var json = JsonSerializer.Serialize(saveFile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public SaveFile? Load(int slot = 0)
    {
        var path = SlotPath(slot);
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SaveFile>(json, JsonOptions);
    }

    public bool SlotExists(int slot = 0) => File.Exists(SlotPath(slot));

    public void Delete(int slot = 0)
    {
        var path = SlotPath(slot);
        if (File.Exists(path)) File.Delete(path);
    }

    private string SlotPath(int slot) => Path.Combine(_saveDirectory, $"slot{slot}.json");
}
