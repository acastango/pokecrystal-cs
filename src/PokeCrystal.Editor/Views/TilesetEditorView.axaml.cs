using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PokeCrystal.Editor.Views;

public sealed partial class TilesetEditorView : UserControl
{
    public TilesetEditorView() => AvaloniaXamlLoader.Load(this);
}
