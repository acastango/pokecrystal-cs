namespace PokeCrystal.Editor.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PokeCrystal.World;

/// <summary>
/// Custom Avalonia control that renders the map as a colored block grid.
/// Each block is drawn as a filled rectangle. The cursor tile is highlighted.
/// Geometry-aware: cell size is derived from map dimensions vs control size.
///
/// Full implementation would blit tile textures from the loaded tileset sprites.
/// This scaffold uses color-coded blocks so the editor is functional without
/// a loaded texture pipeline.
/// </summary>
public sealed class MapCanvas : Control
{
    // --- Avalonia properties ---

    public static readonly StyledProperty<MapData?> MapDataProperty =
        AvaloniaProperty.Register<MapCanvas, MapData?>(nameof(MapData));

    public static readonly StyledProperty<int> CursorXProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(CursorX));

    public static readonly StyledProperty<int> CursorYProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(CursorY));

    /// <summary>Raised when the user clicks or drags on a tile.</summary>
    public event EventHandler<TileClickedEventArgs>? TileClicked;

    public MapData? MapData
    {
        get => GetValue(MapDataProperty);
        set => SetValue(MapDataProperty, value);
    }

    public int CursorX
    {
        get => GetValue(CursorXProperty);
        set => SetValue(CursorXProperty, value);
    }

    public int CursorY
    {
        get => GetValue(CursorYProperty);
        set => SetValue(CursorYProperty, value);
    }

    // External block data provider — set by the view when a map is loaded
    public Func<int, int, byte>? GetBlock { get; set; }

    static MapCanvas()
    {
        MapDataProperty.Changed.AddClassHandler<MapCanvas>((c, _) => c.InvalidateVisual());
        CursorXProperty.Changed.AddClassHandler<MapCanvas>((c, _) => c.InvalidateVisual());
        CursorYProperty.Changed.AddClassHandler<MapCanvas>((c, _) => c.InvalidateVisual());
        AffectsRender<MapCanvas>(MapDataProperty, CursorXProperty, CursorYProperty);
    }

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------

    public override void Render(DrawingContext ctx)
    {
        var map = MapData;
        if (map is null || map.Width == 0 || map.Height == 0) return;

        double cellW = Bounds.Width  / map.Width;
        double cellH = Bounds.Height / map.Height;

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                byte block = GetBlock?.Invoke(x, y) ?? 0;
                var fill   = BlockColor(block);
                var rect   = new Rect(x * cellW, y * cellH, cellW, cellH);
                ctx.FillRectangle(fill, rect);
                ctx.DrawRectangle(Pens.Grid, rect);
            }
        }

        // Cursor highlight
        var cursorRect = new Rect(CursorX * cellW, CursorY * cellH, cellW, cellH);
        ctx.DrawRectangle(Pens.Cursor, cursorRect);
    }

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        FireTileEvent(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            FireTileEvent(e.GetPosition(this));
    }

    private void FireTileEvent(Point pos)
    {
        var map = MapData;
        if (map is null || map.Width == 0 || map.Height == 0) return;

        double cellW = Bounds.Width  / map.Width;
        double cellH = Bounds.Height / map.Height;
        int tx = (int)(pos.X / cellW);
        int ty = (int)(pos.Y / cellH);

        if (tx < 0 || ty < 0 || tx >= map.Width || ty >= map.Height) return;

        CursorX = tx;
        CursorY = ty;
        TileClicked?.Invoke(this, new TileClickedEventArgs(tx, ty));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IBrush BlockColor(byte block) => block switch
    {
        0   => Brushes.DimGray,
        1   => Brushes.ForestGreen,
        2   => Brushes.SaddleBrown,
        3   => Brushes.SteelBlue,
        4   => Brushes.DarkSlateGray,
        _   => new SolidColorBrush(Color.FromRgb(
                   (byte)((block * 37) % 200 + 30),
                   (byte)((block * 71) % 200 + 30),
                   (byte)((block * 113) % 200 + 30)))
    };

    private static class Pens
    {
        public static readonly IPen Grid   = new Pen(Brushes.Black, 0.5);
        public static readonly IPen Cursor = new Pen(Brushes.Yellow, 2.0);
    }
}

public sealed class TileClickedEventArgs(int x, int y) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
}
