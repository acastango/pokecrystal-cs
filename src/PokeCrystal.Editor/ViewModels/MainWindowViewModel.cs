namespace PokeCrystal.Editor.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeCrystal.Data;
using PokeCrystal.World;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _projectPath = string.Empty;
    [ObservableProperty] private string _statusMessage = "No project loaded.";
    [ObservableProperty] private int _selectedTabIndex;

    public MapEditorViewModel      MapEditor      { get; } = new();
    public EventEditorViewModel    EventEditor    { get; } = new();
    public EncounterEditorViewModel EncounterEditor { get; } = new();
    public TilesetEditorViewModel  TilesetEditor  { get; } = new();

    private DataRegistry?  _dataRegistry;
    private MapRegistry?   _mapRegistry;

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        // Path selection delegated to the view via a service in a full implementation.
        // For now, prompt via a hardcoded fallback so the VM stays testable.
        var path = await PickFolderAsync();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            LoadProject(path);
            ProjectPath   = path;
            StatusMessage = $"Loaded: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading project: {ex.Message}";
        }
    }

    private void LoadProject(string dataPath)
    {
        _dataRegistry = (DataRegistry)DataLoader.LoadAll(dataPath);
        _mapRegistry  = new MapRegistry();

        var mapLoader = new MapLoader(_mapRegistry, new PokeCrystal.Scripting.ScriptRegistry());
        mapLoader.LoadAll(System.IO.Path.Combine(dataPath, "maps"));

        MapEditor.Initialize(_mapRegistry, _dataRegistry);
        EventEditor.Initialize(_mapRegistry);
        EncounterEditor.Initialize(_mapRegistry);
        TilesetEditor.Initialize(_dataRegistry);
    }

    // Platform folder picker — returns empty string if cancelled / unavailable.
    private static Task<string> PickFolderAsync()
        => Task.FromResult(string.Empty); // wired to a real dialog in MainWindow.axaml.cs
}
