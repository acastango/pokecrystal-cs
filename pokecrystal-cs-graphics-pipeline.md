# pokecrystal-cs: Graphics and Color Pipeline

## The Problem

Crystal renders through a 2-bit-per-pixel palette system — 4 shades per palette, 8 background palettes and 8 sprite palettes on GBC, each palette being 4 entries of 15-bit RGB (5 bits per channel, 32768 possible colors). Every tile indexes into a palette; every palette is 4 colors. The entire visual output is mediated by this system.

If the renderer only supports palette-indexed rendering, then:
- Mods with hi-res tiles can't use full-color PNGs
- TTF font rendering can't anti-alias (needs more than 4 shades)
- UI themes with hex colors have nowhere to go
- Battle backgrounds can't be full-color illustrations
- Sprite mods can't use 24/32-bit art

But if the renderer ignores palettes entirely, the base game loses palette swaps (time-of-day, flash, poison screen, battle transitions) which are core to how Crystal looks and feels.

The renderer needs to support both: full-color rendering as the default pipeline, with palette-indexed rendering as an optional layer for content that uses it.

---

## Color Modes

Every visual asset declares its color mode. The renderer handles each mode correctly and composites them together.

### Indexed (Palette-Based)

The original mode. Assets are stored as palette-indexed pixel data (2bpp or 4bpp). At render time, the current palette maps indices to actual colors. Palette swaps (time-of-day, flash effects, fade transitions) work by changing the palette, not the pixel data.

Used by: base game tilesets, base game sprites, base game UI frames.

### Direct Color (Full RGB/RGBA)

Modern mode. Assets are stored as full-color images (PNG, typically 32-bit RGBA). Rendered directly — no palette lookup. Supports transparency, anti-aliasing, smooth gradients, and arbitrary color counts.

Used by: mod tilesets, TTF font glyphs, hi-res sprites, full-color battle backgrounds, UI elements with hex color themes.

### Hybrid

A single scene can mix both modes. The base game's palette-indexed overworld tiles render through the palette system (so time-of-day palette swaps work), while a mod's full-color UI overlay renders directly. The compositor handles both in the same frame.

---

## Schema (Layer 0)

```csharp
public enum ColorMode
{
    Indexed,    // Palette-based (2bpp, 4bpp, or 8bpp indexed)
    Direct      // Full RGBA (8 bits per channel, 32-bit)
}

public record PaletteEntry(byte R, byte G, byte B);

public record Palette(
    string Id,
    PaletteEntry[] Colors    // 4 entries for GB-style, up to 256 for extended
);

public record PaletteSet(
    string Id,
    Dictionary<string, Palette> Palettes   // Named palettes: "bg0"–"bg7", "obj0"–"obj7", or custom names
);

public record PaletteProfile(
    string Id,
    string Description,
    Dictionary<string, string> PaletteSetOverrides
    // Maps context keys ("morning", "day", "night", "cave", "flash") to PaletteSet IDs
);

public record SpriteSheet(
    string Id,
    string File,                // PNG file path
    ColorMode ColorMode,
    int BitsPerPixel,           // 2, 4, 8 (indexed) or 32 (direct)
    string PaletteId,           // Which palette to use (indexed mode only, null for direct)
    int CellWidth,              // Individual sprite/tile width in pixels
    int CellHeight,             // Individual sprite/tile height in pixels
    bool HasAlpha               // For direct color: does the PNG have transparency?
);

public record TilesetGraphics(
    string Id,
    string SpriteSheetId,       // Reference to the sprite sheet
    ColorMode ColorMode,
    string DefaultPaletteSetId, // For indexed mode: which palette set to start with
    List<AnimatedTile> AnimatedTiles  // Optional animated tile definitions
);

public record AnimatedTile(
    int TileIndex,
    int FrameCount,
    int FrameMs
);

public record ColorConfig(
    int MaxPalettes,            // 8 for GB-faithful, uncapped for mods
    int ColorsPerPalette,       // 4 for GB-faithful, up to 256 for extended
    int BitsPerChannel,         // 5 for GBC-faithful (15-bit), 8 for modern (24/32-bit)
    bool SupportsPaletteSwap,   // Can palettes be swapped at runtime?
    bool SupportsAlpha          // Can sprites have per-pixel transparency?
);
```

---

## Data (Layer 1)

### Color Configuration

Global config that sets the rendering capabilities:

```json
{
  "graphics": {
    "color_config": {
      "max_palettes": 16,
      "colors_per_palette": 4,
      "bits_per_channel": 5,
      "supports_palette_swap": true,
      "supports_alpha": true
    },
    "default_palette_profile": "gbc_faithful"
  }
}
```

A mod pushing to full color:

```json
{
  "graphics": {
    "color_config": {
      "max_palettes": 256,
      "colors_per_palette": 256,
      "bits_per_channel": 8,
      "supports_palette_swap": true,
      "supports_alpha": true
    },
    "default_palette_profile": "full_color"
  }
}
```

### Palette Definitions

Base game palettes extracted from Crystal:

```json
{
  "id": "gbc_day",
  "palettes": {
    "bg0": { "colors": [{"r":248,"g":248,"b":248}, {"r":168,"g":200,"b":136}, {"r":80,"g":128,"b":64}, {"r":0,"g":0,"b":0}] },
    "bg1": { "colors": [{"r":248,"g":248,"b":248}, {"r":200,"g":168,"b":136}, {"r":128,"g":80,"b":64}, {"r":0,"g":0,"b":0}] },
    "obj0": { "colors": [{"r":0,"g":0,"b":0}, {"r":248,"g":56,"b":0}, {"r":248,"g":168,"b":56}, {"r":248,"g":248,"b":248}] }
  }
}
```

Time-of-day palette profile:

```json
{
  "id": "gbc_faithful",
  "description": "Matches Crystal's GBC palette behavior",
  "palette_set_overrides": {
    "morning": "gbc_morning",
    "day": "gbc_day",
    "night": "gbc_night",
    "cave": "gbc_cave",
    "flash_active": "gbc_flash",
    "battle_intro": "gbc_battle_flash"
  }
}
```

### Sprite Sheet Declaration

Base game sprite (indexed, palette-based):

```json
{
  "id": "player_sprite",
  "file": "sprites/player.png",
  "color_mode": "indexed",
  "bits_per_pixel": 2,
  "palette_id": "obj0",
  "cell_width": 16,
  "cell_height": 16,
  "has_alpha": false
}
```

Mod sprite (full color, RGBA):

```json
{
  "id": "player_sprite_hd",
  "file": "sprites/player_hd.png",
  "color_mode": "direct",
  "bits_per_pixel": 32,
  "palette_id": null,
  "cell_width": 32,
  "cell_height": 32,
  "has_alpha": true
}
```

---

## Rendering Pipeline (Layer 5)

### Architecture

The renderer composites from multiple sources, each potentially in a different color mode:

```
Indexed tiles (palette lookup) ──┐
                                 ├──→ Compositor ──→ Final RGBA framebuffer ──→ Scale + Display
Direct color tiles ──────────────┤
Direct color sprites ────────────┤
Indexed sprites (palette lookup) ┤
UI layer (direct color) ─────────┘
```

Everything converges into a single RGBA framebuffer. The compositor doesn't care whether a pixel came from a palette lookup or a direct-color source — by the time it hits the framebuffer, it's RGBA.

### Palette-Indexed Rendering Path

1. Read pixel data from tile/sprite (2bpp or 4bpp index values)
2. Look up the current palette for this tile/sprite
3. Map index → `PaletteEntry` → RGBA color
4. Write RGBA to the framebuffer

Palette swaps work by changing which palette is active. A time-of-day transition loads a new `PaletteSet`; every indexed tile and sprite immediately renders with the new colors on the next frame. The pixel data never changes — only the lookup table.

This is how the base game achieves: morning/day/night tinting, cave darkness, flash lightening dark caves, poison screen pulse, battle intro flash sequences, and fade-to-black/white transitions.

### Direct Color Rendering Path

1. Read pixel data from tile/sprite (RGBA)
2. Write directly to framebuffer (with alpha compositing if applicable)

No palette involved. Time-of-day effects on direct-color assets use a post-processing tint instead of palette swap (see below).

### Post-Processing for Direct Color

Direct-color assets can't participate in palette swaps, but they still need to respond to environmental effects. The renderer applies post-processing to direct-color content:

**Time-of-day tint:** A color overlay applied after rendering. Morning = warm amber tint, night = cool blue tint, etc. The tint color and intensity are configurable per time period:

```json
{
  "time_tints": {
    "morning": { "color": "#FFD080", "intensity": 0.15 },
    "day": { "color": "#FFFFFF", "intensity": 0.0 },
    "night": { "color": "#4060A0", "intensity": 0.3 }
  }
}
```

For indexed content, the palette already encodes the tint (different palette sets for different times). For direct content, the post-process tint achieves the same visual effect. Both paths produce a consistent result.

**Flash/fade:** Full-screen effects that work regardless of color mode. Fade-to-black multiplies all pixels toward (0,0,0). Fade-to-white multiplies toward (255,255,255). Flash is a brief white overlay at configurable opacity.

**Poison/damage pulse:** A brief red overlay. Same mechanism as time-of-day tint but triggered by game events and fading quickly.

### Mixed-Mode Compositing

A single frame might contain:
- Indexed overworld tiles (palette-swapped for time of day)
- Direct-color NPC sprites from a mod (tinted by post-processing)
- Indexed player sprite (palette-swapped)
- Direct-color UI overlay (no tint — UI stays clean)
- Direct-color text with anti-aliased TTF glyphs

The compositor handles this by rendering in layers:

1. **Background tiles** — indexed or direct, per tileset color mode
2. **Sprite layer** — each sprite rendered according to its own color mode
3. **Overlay tiles** — tall grass, bridges (same mode as the tileset)
4. **Post-process** — time-of-day tint applied to layers 1–3 (not UI)
5. **UI layer** — always direct color, no post-processing tint
6. **Scale and display** — the final framebuffer is scaled per ViewportConfig

The post-process tint boundary between "world" (layers 1–3) and "UI" (layer 5) is the key architectural decision. UI must not be tinted by time-of-day or it becomes unreadable at night.

---

## Palette Effects System

Palette manipulation is how Crystal achieves most of its visual effects. The engine preserves this capability for indexed content and extends it for modding.

### Runtime Palette Operations

```csharp
public interface IPaletteManager
{
    void LoadPaletteSet(string paletteSetId);
    void SetPalette(string slotName, Palette palette);
    Palette GetPalette(string slotName);

    // Transitions
    void FadeToBlack(float durationSec);
    void FadeToWhite(float durationSec);
    void FadeFromBlack(float durationSec);
    void FadeFromWhite(float durationSec);
    void CrossfadePaletteSet(string targetSetId, float durationSec);

    // Per-palette manipulation
    void FlashPalette(string slotName, PaletteEntry color, float durationSec);
    void CyclePalette(string slotName, int offset);  // Rotate colors within palette

    // For direct-color content
    void SetWorldTint(PaletteEntry color, float intensity);
    void FadeWorldTint(PaletteEntry targetColor, float targetIntensity, float durationSec);
}
```

Lua scripts access palette effects:

```lua
fade_to_black(0.5)
fade_from_black(0.5)
set_world_tint("#4060A0", 0.3)   -- manual night effect
flash_screen("#FFFFFF", 0.2)      -- white flash for evolution
```

### Palette Animation

Some effects cycle palette entries over time — water shimmer, lava glow, neon signs. The palette manager supports scheduled palette rotations:

```json
{
  "palette_animations": [
    {
      "palette_slot": "bg3",
      "type": "cycle",
      "indices": [1, 2, 3],
      "frame_ms": 200
    }
  ]
}
```

This rotates colors at indices 1, 2, and 3 in bg3 every 200ms — a 3-frame water animation using palette cycling, just like the original. Direct-color tilesets achieve the same effect with animated tiles instead (frame-based tile swapping, defined in TilesetGraphics).

---

## Sprite Transparency

### Indexed Sprites

In Crystal, palette index 0 is transparent for sprites (OBJ layer). The renderer preserves this: any pixel with index 0 is not drawn. This is automatic for indexed-mode sprites.

### Direct Color Sprites

RGBA sprites use the alpha channel. Fully transparent pixels (A=0) are not drawn. Semi-transparent pixels (0 < A < 255) are alpha-composited. This enables smooth sprite edges, glow effects, and partial transparency that the GB could never do.

### Interaction

An indexed sprite and a direct-color sprite can coexist on the same screen. The compositor handles transparency correctly for both — index-0 skipping for indexed sprites, alpha blending for direct sprites. The final framebuffer always has full alpha support.

---

## Content Pipeline Integration

### Indexed Asset Ingestion

The extraction tooling outputs Crystal's graphics as indexed PNGs — the PNG pixel values ARE the palette indices (0–3 for 2bpp). The content pipeline loads these and preserves the index data. At render time, the palette manager supplies the current palette for lookup.

Mod-created indexed assets follow the same convention: a PNG where pixel values are palette indices, accompanied by a sprite sheet JSON declaring `"color_mode": "indexed"` and the target palette.

### Direct Color Asset Ingestion

Standard RGBA PNGs. The content pipeline loads them as MonoGame `Texture2D` objects. No conversion needed. Modders author in any image editor and export as PNG.

### Automatic Mode Detection

If a sprite sheet JSON doesn't specify `color_mode`, the content pipeline auto-detects:
- PNG with 4 or fewer unique colors and no alpha channel → `indexed` (assumes palette-based)
- PNG with alpha channel or more than 16 unique colors → `direct`
- Ambiguous cases → `direct` (safer default)

Explicit declaration in JSON always overrides auto-detection.

---

## Mod Surface

- **Data:** palette set definitions (JSON), palette profiles for time-of-day, tint configs, sprite sheets with declared color modes, color config overrides
- **Overridable:** `IPaletteManager` for custom palette effects. `IPostProcessor` interface for custom screen-space effects (CRT scanlines, bloom, color grading — these are plugin territory)
- **Hardcoded:** the compositor layer ordering (background → sprites → overlay → post-process → UI), the RGBA framebuffer format, the MonoGame texture pipeline

---

## Configuration Summary

```json
{
  "graphics": {
    "color_config": {
      "max_palettes": 16,
      "colors_per_palette": 4,
      "bits_per_channel": 5,
      "supports_palette_swap": true,
      "supports_alpha": true
    },
    "default_palette_profile": "gbc_faithful",
    "time_tints": {
      "morning": { "color": "#FFD080", "intensity": 0.15 },
      "day": { "color": "#FFFFFF", "intensity": 0.0 },
      "night": { "color": "#4060A0", "intensity": 0.3 }
    },
    "post_processing": {
      "tint_applies_to_ui": false,
      "fade_applies_to_ui": true
    }
  }
}
```

The distinction between `tint_applies_to_ui: false` and `fade_applies_to_ui: true` is important: time-of-day tint should NOT affect menus and text (readability), but fade-to-black SHOULD affect everything (screen transition). These are separately configurable so mods can adjust the behavior.

---

## What This Does NOT Cover

- **Shader effects.** Post-processing shaders (CRT filter, bloom, color grading) are plugin territory via `IPostProcessor`. The base engine doesn't ship with any.
- **Particle systems.** Weather particles (rain, snow, sandstorm) are a rendering concern but are separate from the color pipeline. They'd be a Layer 5 component that renders direct-color particles into the sprite layer.
- **3D rendering.** The engine is 2D. The color pipeline is a 2D compositing pipeline. No depth buffers, no 3D transforms.
