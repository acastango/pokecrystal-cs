namespace PokeCrystal.Schema;

public record FontData(
    string Id,
    FontType Type,
    string File,
    string? MetricsFile,
    int DefaultSize,
    int LineSpacing,
    bool IsMonospace
) : IIdentifiable;

public enum FontType { Bitmap, Ttf }

public record GlyphMetrics(
    char Char,
    int SheetX,
    int SheetY,
    int Width,
    int Height,
    int Advance,
    int OffsetX,
    int OffsetY
);

public record TextStyle(
    string FontId,
    int Size,
    uint Color,
    bool Bold,
    bool Italic,
    int LetterSpacing,
    float LineSpacingMultiplier
);

public record TextBoxStyle(
    FrameStyle FrameStyle,
    string? FrameSpriteSheet,
    int FrameTileSize,
    uint BackgroundColor,
    float BackgroundOpacity,
    uint TextColor,
    string TextFontId,
    int TextSize,
    int PaddingX,
    int PaddingY,
    string? ScrollArrowSprite,
    int ScrollArrowX,
    int ScrollArrowY
);

public enum FrameStyle { NineSlice, SimpleBorder, None }
