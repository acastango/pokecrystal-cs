namespace PokeCrystal.Editor.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeCrystal.World;

public sealed partial class EncounterEditorViewModel : ObservableObject
{
    [ObservableProperty] private string?  _selectedMapId;
    [ObservableProperty] private int      _mornRate;
    [ObservableProperty] private int      _dayRate;
    [ObservableProperty] private int      _niteRate;
    [ObservableProperty] private int      _waterRate;

    public ObservableCollection<string>       AvailableMapIds { get; } = new();
    public ObservableCollection<WildSlotRow>  GrassSlotsMorn  { get; } = new();
    public ObservableCollection<WildSlotRow>  GrassSlotsDay   { get; } = new();
    public ObservableCollection<WildSlotRow>  GrassSlotsNite  { get; } = new();
    public ObservableCollection<WildSlotRow>  WaterSlots      { get; } = new();

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
        SelectedMapId = mapId;

        GrassSlotsMorn.Clear();
        GrassSlotsDay.Clear();
        GrassSlotsNite.Clear();
        WaterSlots.Clear();

        if (map.WildGrass is { } grass)
        {
            MornRate = grass.MornRate;
            DayRate  = grass.DayRate;
            NiteRate = grass.NiteRate;
            foreach (var s in grass.Morn) GrassSlotsMorn.Add(new(s.Level, s.SpeciesId));
            foreach (var s in grass.Day)  GrassSlotsDay.Add(new(s.Level, s.SpeciesId));
            foreach (var s in grass.Nite) GrassSlotsNite.Add(new(s.Level, s.SpeciesId));
        }
        else { MornRate = DayRate = NiteRate = 0; }

        if (map.WildWater is { } water)
        {
            WaterRate = water.Rate;
            foreach (var s in water.Slots) WaterSlots.Add(new(s.Level, s.SpeciesId));
        }
        else { WaterRate = 0; }
    }

    [RelayCommand]
    private void AddGrassSlot(string time)
    {
        var row = new WildSlotRow(5, string.Empty);
        switch (time)
        {
            case "morn": GrassSlotsMorn.Add(row); break;
            case "day":  GrassSlotsDay.Add(row);  break;
            default:     GrassSlotsNite.Add(row); break;
        }
    }

    [RelayCommand]
    private void SaveEncounters()
    {
        // Serializes back to data/maps/{id}.json when MapLoader gains write support.
    }
}

/// <summary>Editable row in an encounter table.</summary>
public sealed partial class WildSlotRow : ObservableObject
{
    [ObservableProperty] private int    _level;
    [ObservableProperty] private string _speciesId;

    public WildSlotRow(int level, string speciesId)
    {
        _level     = level;
        _speciesId = speciesId;
    }
}
