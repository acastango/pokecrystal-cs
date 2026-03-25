namespace PokeCrystal.Editor.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeCrystal.World;

public sealed partial class EventEditorViewModel : ObservableObject
{
    [ObservableProperty] private string?         _selectedMapId;
    [ObservableProperty] private NpcData?        _selectedNpc;
    [ObservableProperty] private MapData?        _currentMap;

    public ObservableCollection<string>  AvailableMapIds { get; } = new();
    public ObservableCollection<NpcData> Npcs            { get; } = new();

    private MapRegistry? _maps;

    public void Initialize(MapRegistry maps)
    {
        _maps = maps;
        AvailableMapIds.Clear();
        foreach (var map in maps.All.OrderBy(m => m.Id))
            AvailableMapIds.Add(map.Id);
    }

    [RelayCommand]
    private void LoadMap(string mapId)
    {
        if (_maps is null || !_maps.TryGet(mapId, out var map) || map is null) return;
        CurrentMap    = map;
        SelectedMapId = mapId;
        Npcs.Clear();
        foreach (var npc in map.Npcs)
            Npcs.Add(npc);
    }

    [RelayCommand]
    private void AddNpc()
    {
        if (CurrentMap is null) return;
        var npc = new NpcData(
            Id:           Npcs.Count + 1,
            SpriteId:     "default",
            X:            0,
            Y:            0,
            MovementType: "Stationary",
            ScriptId:     string.Empty,
            Hidden:       false);
        Npcs.Add(npc);
    }

    [RelayCommand]
    private void RemoveNpc(NpcData npc) => Npcs.Remove(npc);

    /// <summary>Move selected NPC to (x, y).</summary>
    public void MoveNpc(NpcData npc, int x, int y)
    {
        int idx = Npcs.IndexOf(npc);
        if (idx < 0) return;
        Npcs[idx] = npc with { X = x, Y = y };
        if (SelectedNpc == npc) SelectedNpc = Npcs[idx];
    }

    [RelayCommand]
    private void SaveMap()
    {
        // Persists Npcs back to disk when MapLoader gains write support.
    }
}
