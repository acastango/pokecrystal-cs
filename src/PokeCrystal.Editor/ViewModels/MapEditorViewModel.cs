namespace PokeCrystal.Editor.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeCrystal.Data;
using PokeCrystal.World;

public sealed partial class MapEditorViewModel : ObservableObject
{
    // --- State ---
    [ObservableProperty] private MapData? _currentMap;
    [ObservableProperty] private string?  _selectedMapId;
    [ObservableProperty] private int      _cursorX;
    [ObservableProperty] private int      _cursorY;
    [ObservableProperty] private byte     _selectedBlock;
    [ObservableProperty] private bool     _canUndo;
    [ObservableProperty] private bool     _canRedo;

    public ObservableCollection<string> AvailableMapIds { get; } = new();

    // Block data buffer: index = y*width+x, value = block id
    private byte[]? _blockData;

    private MapRegistry?  _maps;
    private DataRegistry? _data;

    private readonly Stack<IEditorAction> _undoStack = new();
    private readonly Stack<IEditorAction> _redoStack = new();

    public void Initialize(MapRegistry maps, DataRegistry data)
    {
        _maps = maps;
        _data = data;
        AvailableMapIds.Clear();
        foreach (var map in maps.All.OrderBy(m => m.Id))
            AvailableMapIds.Add(map.Id);
    }

    [RelayCommand]
    private void LoadMap(string mapId)
    {
        if (_maps is null || !_maps.TryGet(mapId, out var map) || map is null) return;

        CurrentMap   = map;
        SelectedMapId = mapId;
        _blockData   = new byte[map.Width * map.Height];
        _undoStack.Clear();
        _redoStack.Clear();
        RefreshUndoRedo();
    }

    /// <summary>Paint the selected block at (x, y). Pushes an undo action.</summary>
    [RelayCommand]
    private void PaintBlock(Point2D pos)
    {
        if (CurrentMap is null || _blockData is null) return;
        int idx = pos.Y * CurrentMap.Width + pos.X;
        if (idx < 0 || idx >= _blockData.Length) return;

        byte prev = _blockData[idx];
        byte next = SelectedBlock;
        if (prev == next) return;

        var action = new PaintBlockAction(_blockData, idx, prev, next);
        action.Do();
        _undoStack.Push(action);
        _redoStack.Clear();
        RefreshUndoRedo();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        RefreshUndoRedo();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack.Pop();
        action.Do();
        _undoStack.Push(action);
        RefreshUndoRedo();
    }

    [RelayCommand]
    private void SaveMap()
    {
        // Map data is currently in-memory. Full save writes back to data/maps/{id}.json.
        // Wired in a future iteration when MapLoader gains a Save() method.
    }

    private void RefreshUndoRedo()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
    }

    public byte GetBlock(int x, int y)
    {
        if (_blockData is null || CurrentMap is null) return 0;
        int idx = y * CurrentMap.Width + x;
        return idx >= 0 && idx < _blockData.Length ? _blockData[idx] : (byte)0;
    }
}

/// <summary>Simple (x, y) pair used as a command parameter from the MapCanvas.</summary>
public readonly record struct Point2D(int X, int Y);

/// <summary>Undo/redo action for a single block paint.</summary>
file sealed class PaintBlockAction : IEditorAction
{
    private readonly byte[] _data;
    private readonly int    _idx;
    private readonly byte   _prev;
    private readonly byte   _next;

    public PaintBlockAction(byte[] data, int idx, byte prev, byte next)
    {
        _data = data; _idx = idx; _prev = prev; _next = next;
    }

    public string Description => $"Paint block {_next} at index {_idx}";
    public void Do()   => _data[_idx] = _next;
    public void Undo() => _data[_idx] = _prev;
}
