using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PokeCrystal.Editor.Views;

public sealed partial class EncounterEditorView : UserControl
{
    public EncounterEditorView() => AvaloniaXamlLoader.Load(this);
}
