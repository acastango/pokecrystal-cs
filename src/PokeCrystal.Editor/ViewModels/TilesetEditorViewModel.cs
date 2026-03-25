namespace PokeCrystal.Editor.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeCrystal.Data;
using PokeCrystal.Schema;

public sealed partial class TilesetEditorViewModel : ObservableObject
{
    [ObservableProperty] private SpriteSheet? _selectedTileset;
    [ObservableProperty] private string       _statusMessage = string.Empty;

    public ObservableCollection<SpriteSheet> AvailableTilesets { get; } = new();

    private DataRegistry? _data;

    public void Initialize(DataRegistry data)
    {
        _data = data;
        AvailableTilesets.Clear();
        foreach (var sheet in data.GetAll<SpriteSheet>().OrderBy(s => s.Id))
            AvailableTilesets.Add(sheet);
    }

    /// <summary>
    /// Import a PNG as a new SpriteSheet and register it into the DataRegistry.
    /// Full implementation opens a file dialog and reads the image dimensions.
    /// </summary>
    [RelayCommand]
    private void ImportPng(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || _data is null)
        {
            StatusMessage = "No file selected.";
            return;
        }

        var id = System.IO.Path.GetFileNameWithoutExtension(filePath);
        var sheet = new SpriteSheet(
            Id:          id,
            File:        filePath,
            ColorMode:   ColorMode.Direct,
            BitsPerPixel: 32,
            PaletteId:   null,
            CellWidth:   16,
            CellHeight:  16,
            HasAlpha:    true);

        _data.Register(sheet);
        AvailableTilesets.Add(sheet);
        SelectedTileset = sheet;
        StatusMessage   = $"Imported: {id}";
    }
}
