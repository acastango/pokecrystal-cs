# pokecrystal-cs: Adaptability Addendum

Five systems that must be data-driven from day one. Retrofitting any of these after the renderer, UI, or input system is built means rewriting the layer that depends on them. Spec them in Layer 0 (Schema) and Layer 1 (Data), consume them in Layers 5–6.

---

## 1. Resolution and Viewport Scaling

### The Problem

Crystal runs at 160×144, displayed on modern monitors at some integer multiple. If the renderer hardcodes this, mods can't target higher internal resolutions, different aspect ratios, or flexible window sizing. A modder making a widescreen GBA-style game or a hi-res tileset mod needs the resolution pipeline to be config, not code.

### Schema (Layer 0)

```csharp
public record ViewportConfig(
    int InternalWidth = 160,
    int InternalHeight = 144,
    int ScaleFactor = 3,
    ScaleMode ScaleMode = ScaleMode.IntegerNearest,
    AspectMode AspectMode = AspectMode.Fixed,
    bool AllowResize = true
);

public enum ScaleMode
{
    IntegerNearest,    // Pixel-perfect, integer multiples only
    NearestNeighbor,   // Arbitrary scale, sharp pixels
    Bilinear           // Arbitrary scale, smooth (for hi-res mods)
}

public enum AspectMode
{
    Fixed,             // Letterbox/pillarbox to maintain aspect ratio
    Stretch,           // Fill window, distort if needed
    Expand             // Show more of the world when window is wider/taller
}
```

### Data (Layer 1)

Lives in the game config:

```json
{
  "viewport": {
    "internal_width": 160,
    "internal_height": 144,
    "scale_factor": 3,
    "scale_mode": "integer_nearest",
    "aspect_mode": "fixed",
    "allow_resize": true
  }
}
```

### How It Flows

The renderer (Layer 5) creates a `RenderTarget2D` at `InternalWidth × InternalHeight`. All game rendering targets this surface — tiles, sprites, UI, everything. At display time, the render target is scaled to the window using the configured `ScaleMode`. If `AspectMode` is `Fixed`, black bars fill the remainder. If `Expand`, the camera reveals more tiles (world engine needs to handle this — the scroll buffer and camera bounds derive from viewport dimensions).

The internal resolution determines how many tiles are visible: `tiles_x = InternalWidth / tile_size`, `tiles_y = InternalHeight / tile_size`. This ties directly to the adaptable tile system — a mod with 16px tiles at 320×288 shows the same 20×18 tile grid as 8px tiles at 160×144, just at higher fidelity.

### Mod Surface

- Data: ViewportConfig in config JSON
- Overridable: none needed — the config handles everything
- Hardcoded: the render target → scale → display pipeline itself

---

## 2. Text and Font System

### The Problem

Crystal uses a fixed-width 8×8 tile font baked into VRAM — one character set, one size, no variation. If the text renderer assumes this, mods can never use variable-width fonts, larger glyphs, different alphabets, rich text (color, bold), or multiple font sizes. Localization into languages with large character sets (CJK, Arabic, Thai) is impossible.

### Schema (Layer 0)

```csharp
public record FontData(
    string Id,
    string Type,               // "bitmap" or "ttf"
    string File,               // Sprite sheet PNG or TTF/OTF file
    string MetricsFile,        // JSON glyph metrics (for bitmap fonts)
    int DefaultSize,           // Pixel height
    int LineSpacing,           // Pixels between baselines
    bool IsMonospace
);

public record GlyphMetrics(
    char Character,
    int X, int Y,              // Position on sprite sheet (bitmap only)
    int Width, int Height,     // Glyph dimensions
    int Advance,               // Horizontal advance after this glyph
    int OffsetX, int OffsetY   // Rendering offset from cursor
);

public record TextStyle(
    string FontId,
    int Size,
    string Color,              // CSS-style color key or hex
    bool Bold,
    bool Italic,
    float LetterSpacing,
    float LineSpacingMultiplier
);

public record TextBoxStyle(
    string FrameSpriteSheet,   // Sprite sheet for the 9-slice frame
    string BackgroundColor,
    float BackgroundOpacity,
    string TextColor,
    int PaddingTop, int PaddingBottom,
    int PaddingLeft, int PaddingRight,
    string ArrowSprite,        // The "more text" indicator
    int CharsPerLine,          // 0 = auto from box width and font metrics
    int VisibleLines           // How many lines before scrolling
);
```

### Data (Layer 1)

Font definition:

```json
{
  "id": "gb_default",
  "type": "bitmap",
  "file": "fonts/gb_font.png",
  "metrics_file": "fonts/gb_font_metrics.json",
  "default_size": 8,
  "line_spacing": 10,
  "is_monospace": true
}
```

The metrics JSON contains per-glyph data extracted from Crystal's font tiles. For a monospace bitmap font, every glyph has the same width/height/advance, but the format supports variable-width for mod fonts.

TTF font:

```json
{
  "id": "mod_variable_font",
  "type": "ttf",
  "file": "fonts/custom_font.ttf",
  "default_size": 14,
  "line_spacing": 18,
  "is_monospace": false
}
```

### Text Rendering Pipeline

The text renderer (Layer 5) accepts styled text spans and a target rectangle. It lays out glyphs using the font's metrics, handles word wrapping based on the target width, and renders glyph-by-glyph. For bitmap fonts, it blits from the sprite sheet. For TTF fonts, it rasterizes glyphs to a texture cache on first use.

The text engine (Layer 6) handles the game-specific behavior: per-character reveal speed, scroll-on-prompt, choice menus, and text commands (player name insertion, item counts, etc.). It consumes the text renderer but doesn't know about font formats.

### Rich Text

Text content can include inline style markers. A lightweight markup:

```
"This is {color:red}important{/color} and this is {bold}bold{/bold}."
```

Parsed into styled spans at load time. The renderer applies the style per-span. Mods can define named styles in a theme file:

```json
{
  "styles": {
    "damage": { "color": "#FF4444", "bold": true },
    "heal": { "color": "#44FF44" },
    "system": { "color": "#AAAAAA", "italic": true }
  }
}
```

Scripts reference styles by name: `msg("{style:damage}Critical hit!{/style}")`.

### Character Speed

Text reveal speed is configurable and independent of the font system:

```json
{
  "text_speed": {
    "slow": 60,
    "medium": 30,
    "fast": 10,
    "instant": 0
  }
}
```

Values are milliseconds per character. The player selects a speed in options; the text engine reads it from config. A value of 0 reveals all text instantly.

### Mod Surface

- Data: font files (PNG + metrics JSON or TTF), text styles, text box styles, text speed config
- Overridable: `ITextRenderer` interface for completely custom text rendering (e.g., SDF fonts, animated text effects)
- Hardcoded: glyph layout algorithm, text command parsing

---

## 3. Input Mapping

### The Problem

Crystal has 8 inputs: D-pad (4 directions), A, B, Start, Select. If game logic references physical keys or button indices, mods can't add mouse support, touch input, analog stick navigation, additional keybinds, or accessibility remapping. A mod adding a crafting menu might need a dedicated "inventory" keybind. A player using a non-standard controller needs custom mapping.

### Schema (Layer 0)

```csharp
public enum GameAction
{
    // Movement
    MoveUp, MoveDown, MoveLeft, MoveRight,
    
    // Core
    Confirm,        // A button equivalent
    Cancel,         // B button equivalent
    Menu,           // Start equivalent
    RegisteredItem, // Select equivalent
    
    // Extended (mods can register more)
    SpeedToggle,
    QuickSave,
    QuickLoad,
    DebugConsole
}

public record InputBinding(
    GameAction Action,
    List<InputSource> Sources  // Multiple sources per action (keyboard + gamepad)
);

public record InputSource(
    InputDevice Device,    // Keyboard, Gamepad, Mouse
    string Key             // "Z", "Enter", "GamepadA", "MouseLeft", "DPadUp", etc.
);

public interface IInputProvider
{
    bool IsPressed(GameAction action);    // Down this frame
    bool IsHeld(GameAction action);       // Currently held
    bool IsReleased(GameAction action);   // Released this frame
    Vector2 GetAnalogDirection();          // For analog stick, normalized
    Vector2 GetMousePosition();            // Screen coordinates
    void RegisterAction(string name, GameAction action);
}
```

### Data (Layer 1)

Default input config:

```json
{
  "input": {
    "bindings": [
      { "action": "MoveUp",    "sources": [{"device": "keyboard", "key": "Up"},    {"device": "gamepad", "key": "DPadUp"},    {"device": "gamepad", "key": "LeftStickUp"}] },
      { "action": "MoveDown",  "sources": [{"device": "keyboard", "key": "Down"},  {"device": "gamepad", "key": "DPadDown"},  {"device": "gamepad", "key": "LeftStickDown"}] },
      { "action": "MoveLeft",  "sources": [{"device": "keyboard", "key": "Left"},  {"device": "gamepad", "key": "DPadLeft"},  {"device": "gamepad", "key": "LeftStickLeft"}] },
      { "action": "MoveRight", "sources": [{"device": "keyboard", "key": "Right"}, {"device": "gamepad", "key": "DPadRight"}, {"device": "gamepad", "key": "LeftStickRight"}] },
      { "action": "Confirm",        "sources": [{"device": "keyboard", "key": "Z"},      {"device": "gamepad", "key": "A"}] },
      { "action": "Cancel",         "sources": [{"device": "keyboard", "key": "X"},      {"device": "gamepad", "key": "B"}] },
      { "action": "Menu",           "sources": [{"device": "keyboard", "key": "Enter"},  {"device": "gamepad", "key": "Start"}] },
      { "action": "RegisteredItem", "sources": [{"device": "keyboard", "key": "Back"},   {"device": "gamepad", "key": "Select"}] },
      { "action": "SpeedToggle",    "sources": [{"device": "keyboard", "key": "Space"}] },
      { "action": "QuickSave",      "sources": [{"device": "keyboard", "key": "F5"}] },
      { "action": "QuickLoad",      "sources": [{"device": "keyboard", "key": "F9"}] }
    ],
    "analog_deadzone": 0.25,
    "repeat_delay_ms": 400,
    "repeat_rate_ms": 80
  }
}
```

### How It Flows

Game logic (Layers 2–6) never references keys or buttons — only `GameAction` values via `IInputProvider`. The input system (Layer 5) polls MonoGame's `Keyboard`, `GamePad`, and `Mouse` states each frame, maps physical inputs to actions via the binding table, and provides the action state to the game.

`repeat_delay_ms` and `repeat_rate_ms` handle D-pad/keyboard auto-repeat for menu navigation — hold a direction and it fires again after the delay, then at the repeat rate. This replaces the frame-counting approach from the original.

### Mod-Registered Actions

Mods can register new actions beyond the base set. The `GameAction` enum has the core set, but the input system also supports string-keyed actions for extensibility:

```csharp
inputProvider.RegisterAction("crafting_menu", customAction);
// Later:
if (inputProvider.IsPressed("crafting_menu")) { ... }
```

A mod's input bindings are merged with the base config:

```json
{
  "input": {
    "bindings": [
      { "action": "crafting_menu", "sources": [{"device": "keyboard", "key": "C"}] }
    ]
  }
}
```

### Rebinding UI

The options menu includes an input rebinding screen. The player selects an action, presses the desired key/button, and the binding updates. Modified bindings are saved to the player's settings file, separate from the game config. Player settings override game config; game config overrides mod defaults.

### Mod Surface

- Data: input binding config, analog deadzone, repeat timing
- Overridable: `IInputProvider` interface for completely custom input handling (touch screen, motion controls, accessibility devices)
- Hardcoded: the polling loop and MonoGame input state reading

---

## 4. Battle Visual Layout

### The Problem

Crystal's battle layout is fixed: enemy sprite at top-right, player sprite at bottom-left, enemy HP bar at top, player HP bar at bottom, text box at the very bottom. If the renderer hardcodes these positions, a mod can never do side-view battles, double/triple battles, rotation battles, scaled sprites, animated entries, or even just repositioned elements. The battle engine (Layer 2) already emits events — the visual layout should be equally data-driven.

### Schema (Layer 0)

```csharp
public record BattleLayout(
    string Id,
    BattleSlot[] PlayerSlots,
    BattleSlot[] EnemySlots,
    BattleHudPosition PlayerHud,
    BattleHudPosition EnemyHud,
    TextBoxPosition TextBox,
    MenuPosition ActionMenu,
    MenuPosition MoveMenu,
    string BackgroundType        // "wild_grass", "trainer", "cave", etc.
);

public record BattleSlot(
    int X, int Y,               // Sprite anchor position (pixels)
    float Scale,                // Sprite scale (1.0 = native size)
    string Facing,              // "left", "right" — which way the sprite faces
    int EntryOffsetX,           // Off-screen start position for slide-in animation
    int EntryOffsetY,
    float EntryDurationSec
);

public record BattleHudPosition(
    int X, int Y,
    int Width,
    string Alignment             // "left", "right", "center"
);

public record TextBoxPosition(
    int X, int Y,
    int Width, int Height
);

public record MenuPosition(
    int X, int Y,
    int Width, int Height,
    int Columns                  // 1 for vertical list, 2 for grid
);
```

### Data (Layer 1)

Base Crystal layout:

```json
{
  "id": "classic_single",
  "player_slots": [
    { "x": 40, "y": 88, "scale": 1.0, "facing": "right", "entry_offset_x": -80, "entry_offset_y": 0, "entry_duration_sec": 0.5 }
  ],
  "enemy_slots": [
    { "x": 120, "y": 24, "scale": 1.0, "facing": "left", "entry_offset_x": 80, "entry_offset_y": 0, "entry_duration_sec": 0.5 }
  ],
  "player_hud": { "x": 80, "y": 104, "width": 72, "alignment": "right" },
  "enemy_hud": { "x": 8, "y": 16, "width": 72, "alignment": "left" },
  "text_box": { "x": 0, "y": 112, "width": 160, "height": 32 },
  "action_menu": { "x": 80, "y": 112, "width": 80, "height": 32, "columns": 2 },
  "move_menu": { "x": 0, "y": 112, "width": 160, "height": 32, "columns": 2 },
  "background_type": "wild_grass"
}
```

Double battle layout (mod example):

```json
{
  "id": "double_battle",
  "player_slots": [
    { "x": 24, "y": 80, "scale": 0.8, "facing": "right", "entry_offset_x": -60, "entry_offset_y": 0, "entry_duration_sec": 0.5 },
    { "x": 56, "y": 96, "scale": 0.8, "facing": "right", "entry_offset_x": -80, "entry_offset_y": 0, "entry_duration_sec": 0.7 }
  ],
  "enemy_slots": [
    { "x": 104, "y": 16, "scale": 0.8, "facing": "left", "entry_offset_x": 60, "entry_offset_y": 0, "entry_duration_sec": 0.5 },
    { "x": 136, "y": 32, "scale": 0.8, "facing": "left", "entry_offset_x": 80, "entry_offset_y": 0, "entry_duration_sec": 0.7 }
  ],
  "player_hud": { "x": 80, "y": 96, "width": 72, "alignment": "right" },
  "enemy_hud": { "x": 8, "y": 8, "width": 72, "alignment": "left" },
  "text_box": { "x": 0, "y": 112, "width": 160, "height": 32 },
  "action_menu": { "x": 80, "y": 112, "width": 80, "height": 32, "columns": 2 },
  "move_menu": { "x": 0, "y": 112, "width": 160, "height": 32, "columns": 2 },
  "background_type": "trainer"
}
```

### How It Flows

The battle renderer (Layer 5) reads the active `BattleLayout` and positions all elements accordingly. It doesn't know about "top-right" or "bottom-left" — it reads X, Y, and scale from the layout data. Sprite entry animations use the `entry_offset` and `entry_duration` to slide sprites in from off-screen.

The battle engine (Layer 2) doesn't care about layout at all — it emits events (`DamageDealt`, `StatusApplied`, `PokemonFainted`, etc.) and the renderer maps those events to the correct visual slot based on the layout.

Layout selection: the game shell (Layer 6) picks a layout based on the battle type. The mapping from battle type to layout ID is itself configurable:

```json
{
  "battle_layouts": {
    "wild_single": "classic_single",
    "trainer_single": "classic_single",
    "wild_double": "double_battle",
    "trainer_double": "double_battle"
  }
}
```

Mods override this mapping to route any battle type to any layout.

### Battle Backgrounds

Backgrounds are referenced by `background_type` in the layout, which maps to a sprite/animation definition:

```json
{
  "battle_backgrounds": {
    "wild_grass": { "file": "battle/bg_grass.png", "animated": false },
    "trainer": { "file": "battle/bg_trainer.png", "animated": false },
    "cave": { "file": "battle/bg_cave.png", "animated": false },
    "water": { "file": "battle/bg_water.png", "animated": true, "frame_count": 4, "frame_ms": 200 }
  }
}
```

### Mod Surface

- Data: layout definitions (JSON), background definitions, battle type → layout mapping
- Overridable: `IBattleRenderer` interface for completely custom battle presentation (3D battles, animated cutscene-style battles)
- Hardcoded: the event-to-animation mapping pipeline, sprite blitting

---

## 5. Dialogue and UI Theming

### The Problem

Crystal's text boxes use a fixed tile frame, fixed background, fixed text color, and a fixed arrow indicator. Every menu — start menu, party screen, bag, mart, PC — uses the same frame style. If this is hardcoded, mods can't reskin the UI, change the aesthetic, or even replicate later-gen features like customizable window frames. The UI should be theme-driven from the start.

### Schema (Layer 0)

```csharp
public record UITheme(
    string Id,
    TextBoxTheme TextBox,
    MenuTheme Menu,
    HudTheme BattleHud,
    CursorTheme Cursor,
    Dictionary<string, string> ColorPalette  // Named colors for the whole UI
);

public record TextBoxTheme(
    string FrameStyle,          // "9slice", "simple_border", "none"
    string FrameSpriteSheet,    // PNG for 9-slice frame tiles
    int FrameTileSize,          // Tile size in the frame sprite sheet
    string BackgroundColor,
    float BackgroundOpacity,
    string TextColor,
    string TextFont,            // Font ID reference
    int TextSize,
    int PaddingTop, int PaddingBottom,
    int PaddingLeft, int PaddingRight,
    string ScrollArrowSprite,
    int ScrollArrowX, int ScrollArrowY  // Relative to text box
);

public record MenuTheme(
    string FrameStyle,
    string FrameSpriteSheet,
    string BackgroundColor,
    float BackgroundOpacity,
    string TextColor,
    string TextFont,
    int TextSize,
    string SelectedColor,       // Text color when selected
    string SelectedBackground,  // Highlight bar color
    int ItemPaddingX, int ItemPaddingY
);

public record HudTheme(
    string BackgroundColor,
    float BackgroundOpacity,
    string FrameSpriteSheet,
    string HpBarFullColor,
    string HpBarMidColor,      // Below 50%
    string HpBarLowColor,      // Below 25%
    string HpBarBackground,
    string ExpBarColor,
    string ExpBarBackground,
    string LevelTextColor,
    string NameTextColor
);

public record CursorTheme(
    string Sprite,
    int Width, int Height,
    bool Animated,
    int FrameCount,
    int FrameMs
);
```

### Data (Layer 1)

Base Crystal theme:

```json
{
  "id": "crystal_default",
  "text_box": {
    "frame_style": "9slice",
    "frame_sprite_sheet": "ui/frame_default.png",
    "frame_tile_size": 8,
    "background_color": "#FFFFFF",
    "background_opacity": 1.0,
    "text_color": "#000000",
    "text_font": "gb_default",
    "text_size": 8,
    "padding_top": 8, "padding_bottom": 8,
    "padding_left": 8, "padding_right": 8,
    "scroll_arrow_sprite": "ui/arrow_down.png",
    "scroll_arrow_x": 144, "scroll_arrow_y": 24
  },
  "menu": {
    "frame_style": "9slice",
    "frame_sprite_sheet": "ui/frame_default.png",
    "background_color": "#FFFFFF",
    "background_opacity": 1.0,
    "text_color": "#000000",
    "text_font": "gb_default",
    "text_size": 8,
    "selected_color": "#000000",
    "selected_background": "#C8C8C8",
    "item_padding_x": 8, "item_padding_y": 2
  },
  "battle_hud": {
    "background_color": "#F8F8F8",
    "background_opacity": 1.0,
    "frame_sprite_sheet": "ui/hud_frame.png",
    "hp_bar_full_color": "#00C800",
    "hp_bar_mid_color": "#C8C800",
    "hp_bar_low_color": "#C80000",
    "hp_bar_background": "#484848",
    "exp_bar_color": "#4090D0",
    "exp_bar_background": "#484848",
    "level_text_color": "#000000",
    "name_text_color": "#000000"
  },
  "cursor": {
    "sprite": "ui/cursor.png",
    "width": 8, "height": 8,
    "animated": true,
    "frame_count": 2,
    "frame_ms": 400
  },
  "color_palette": {
    "primary": "#000000",
    "secondary": "#484848",
    "accent": "#C80000",
    "background": "#FFFFFF",
    "disabled": "#A0A0A0"
  }
}
```

A mod with a dark theme:

```json
{
  "id": "dark_modern",
  "text_box": {
    "frame_style": "simple_border",
    "background_color": "#1A1A2E",
    "background_opacity": 0.92,
    "text_color": "#E0E0F0",
    "text_font": "mod_variable_font",
    "text_size": 12,
    "padding_top": 10, "padding_bottom": 10,
    "padding_left": 12, "padding_right": 12,
    "scroll_arrow_sprite": "ui/arrow_modern.png",
    "scroll_arrow_x": 140, "scroll_arrow_y": 20
  }
}
```

### 9-Slice Frame Rendering

The classic text box frame is a 3×3 grid of corner and edge tiles that stretches to any size. The frame sprite sheet contains 9 tiles (top-left, top-center, top-right, middle-left, middle-center, middle-right, bottom-left, bottom-center, bottom-right). The renderer tiles the edge and center pieces to fill the target rectangle. `frame_tile_size` tells the renderer how big each tile is in the sprite sheet.

For `"frame_style": "simple_border"`, the renderer draws a solid rect with a 1–2px border instead of using sprite tiles. For `"frame_style": "none"`, just the background fill with no frame.

### Multiple Themes

The game supports multiple loaded themes. The active theme is set globally but can be overridden per-context:

```json
{
  "ui_themes": {
    "default": "crystal_default",
    "battle": "crystal_default",
    "menu": "crystal_default"
  }
}
```

A mod could use one theme for menus and a different theme for battle HUD. The theme selection is configurable per UI context.

The player can select from available themes in the options menu (like the frame selection in Gen 3+). Mods register their themes by including them in the `themes/` data folder.

### Mod Surface

- Data: theme JSON files, frame sprite sheets, cursor sprites, color palettes
- Overridable: `IFrameRenderer` interface for custom frame drawing (shader-based frames, animated borders, procedural backgrounds)
- Hardcoded: 9-slice algorithm, text box layout logic, menu item positioning

---

## Implementation Timing

All five systems define their schemas in **Layer 0** and their data loading in **Layer 1** — both part of Phase 1. The consuming layers (5 and 6) read the config from day one and never hardcode the values these systems parameterize.

| System | Layer 0 (Schema) | Layer 1 (Data) | Layer 5 (Rendering) | Layer 6 (Shell) |
|--------|-----------------|----------------|--------------------|-----------------| 
| Viewport | ViewportConfig | config.json | RenderTarget setup, scaling | Window management |
| Text/Fonts | FontData, GlyphMetrics, TextStyle | Font loader, metrics parser | Text renderer, glyph cache | Text engine (speed, commands) |
| Input | GameAction, InputBinding, IInputProvider | input config.json | MonoGame input polling | Rebinding UI in options |
| Battle Layout | BattleLayout, BattleSlot | Layout JSON files | Battle renderer positioning | Layout selection per battle type |
| UI Theming | UITheme, TextBoxTheme, MenuTheme | Theme JSON loader | Frame renderer, UI drawing | Theme selection in options |

None of these are optional. If any one of them gets hardcoded during implementation and needs to be parameterized later, the refactor touches every file that references the hardcoded value. Define the abstraction in Phase 1, consume it correctly from the first line of rendering code.
