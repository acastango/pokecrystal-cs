using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PokeCrystal.Editor.Views;

public sealed partial class EventEditorView : UserControl
{
    public EventEditorView() => AvaloniaXamlLoader.Load(this);
}
