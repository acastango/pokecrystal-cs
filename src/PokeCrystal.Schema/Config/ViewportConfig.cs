namespace PokeCrystal.Schema;

/// <summary>
/// Renderer viewport and scale settings. Data-driven from config.json.
/// Renderer creates a RenderTarget2D at InternalWidth×InternalHeight (160×144),
/// then scales it to the window size using ScaleMode.
/// </summary>
public record ViewportConfig(
    int InternalWidth,      // 160 (GBC native)
    int InternalHeight,     // 144 (GBC native)
    int ScaleFactor,        // default 3 (480×432 window)
    ScaleMode ScaleMode,
    AspectMode AspectMode,
    bool AllowResize
)
{
    public static ViewportConfig Default => new(160, 144, 3,
        ScaleMode.IntegerNearest, AspectMode.Fixed, AllowResize: true);
}

public enum ScaleMode
{
    IntegerNearest,     // integer scale, point sampling — most authentic
    NearestNeighbor,    // non-integer scale, point sampling
    Bilinear,           // non-integer scale, bilinear filter
}

public enum AspectMode
{
    Fixed,    // letterbox/pillarbox to maintain 160:144
    Stretch,  // fill window (may distort)
    Expand,   // reveal more tiles at wider windows
}
