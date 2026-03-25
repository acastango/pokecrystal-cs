using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PokeCrystal.Editor.Controls;
using PokeCrystal.Editor.ViewModels;

namespace PokeCrystal.Editor.Views;

public sealed partial class MapEditorView : UserControl
{
    public MapEditorView()
    {
        AvaloniaXamlLoader.Load(this);
        var canvas = this.FindControl<MapCanvas>("Canvas")!;
        canvas.TileClicked += OnTileClicked;
        canvas.GetBlock    = (x, y) => (DataContext as MapEditorViewModel)?.GetBlock(x, y) ?? 0;
    }

    private void OnTileClicked(object? sender, TileClickedEventArgs e)
    {
        if (DataContext is MapEditorViewModel vm)
            vm.PaintBlockCommand.Execute(new Point2D(e.X, e.Y));
    }
}
