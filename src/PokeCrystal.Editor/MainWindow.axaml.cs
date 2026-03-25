using Avalonia.Controls;
using Avalonia.Interactivity;
using PokeCrystal.Editor.ViewModels;

namespace PokeCrystal.Editor;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnExitClick(object? sender, RoutedEventArgs e)
        => Close();

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
