namespace PokeCrystal.Schema;

public record UITheme(
    string Id,
    TextBoxTheme TextBox,
    MenuTheme Menu,
    HudTheme Hud,
    CursorTheme Cursor,
    Dictionary<string, uint> ColorPalette
) : IIdentifiable;

public record TextBoxTheme(
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

public record MenuTheme(
    FrameStyle FrameStyle,
    uint BackgroundColor,
    float BackgroundOpacity,
    uint TextColor,
    uint SelectedTextColor,
    uint SelectedBackgroundColor,
    int ItemPaddingX,
    int ItemPaddingY
);

public record HudTheme(
    uint HpFull,
    uint HpMid,
    uint HpLow,
    uint HpBackground,
    uint ExpBarColor,
    uint ExpBarBackground
);

public record CursorTheme(
    string Sprite,
    int Width,
    int Height,
    bool Animated,
    int FrameCount,
    int FrameMs
);
