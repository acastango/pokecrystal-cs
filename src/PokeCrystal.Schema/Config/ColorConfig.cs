namespace PokeCrystal.Schema;

public record ColorConfig(
    int MaxPalettes,
    int ColorsPerPalette,
    int BitsPerChannel
)
{
    public static ColorConfig GbcFaithful => new(8, 4, 5);
    public static ColorConfig Modern      => new(256, 256, 8);
}

public enum ColorMode { Indexed, Direct }

public record Palette(string Id, uint[] Colors) : IIdentifiable;

public record PaletteSet(string Id, List<Palette> Palettes) : IIdentifiable;

public record PaletteProfile(string Id, Dictionary<string, string> ContextToPaletteSetId)
    : IIdentifiable;

public record SpriteSheet(
    string Id,
    string File,
    ColorMode ColorMode,
    int BitsPerPixel,
    string? PaletteId,
    int CellWidth,
    int CellHeight,
    bool HasAlpha
) : IIdentifiable;
