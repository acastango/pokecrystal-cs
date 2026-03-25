# pokecrystal-cs — Complete Architecture Specification

*C# · MonoGame · MoonSharp (Lua) · Avalonia Editor · Hybrid Mod System*

---

## Overview

pokecrystal-cs is a moddable reimplementation of Pokémon Crystal, built in C# on MonoGame. The engine uses Gen 2 mechanics as its baseline but is architected to support arbitrary mechanic changes through a three-tier mod system: data files (JSON), scripts (Lua via MoonSharp), and compiled plugins (C# DLLs). A standalone Avalonia desktop editor provides GUI tools for map editing, tileset authoring, encounter design, and event scripting.

The architecture is organized into 9 layers, ordered by dependency. Each layer only depends on layers below it. Mod hooks exist at every layer. The disassembly (pret/pokecrystal) is the behavioral spec for the base engine; the C# implementation is a clean reimplementation, not a line-by-line port.

---

## Layer Summary

| Layer | Name | Scope |
|-------|------|-------|
| 0 | Data Schema & Constants | Type definitions, enums, JSON schemas, config contracts — everything above references these |
| 1 | Data Layer | JSON/YAML loaders, mod-mergeable data registry, content pipeline for tilesets/sprites/audio |
| 2 | Core Engine | Battle engine, stat calc, type effectiveness, evolution, breeding, PRNG — pure logic, no rendering |
| 3 | Scripting & Events | MoonSharp Lua runtime, event flag system, script command interpreter, NPC behavior |
| 4 | World Engine | Map system, overworld loop, player movement, connections, warps, camera, collision, time-of-day |
| 5 | Rendering & Audio | MonoGame tile renderer, sprite system, UI framework, audio mixer, animation system |
| 6 | Game Shell | Scene manager, save/load, menus (start menu, party, bag, PC, mart), title screen, new game flow |
| 7 | Mod Runtime | Plugin loader (DLL), Lua script binder, data mod merger, hot-reload coordinator, mod dependency resolution |
| 8 | Editor (Standalone) | Avalonia desktop app: map/tileset editor, event placer, encounter editor, script editor, live preview |

## Dependency Graph

| Dependency | Rationale |
|-----------|-----------|
| Layer 0 → nothing | Foundation. No dependencies. |
| Layer 1 → 0 | Data layer loads content into the types/schemas defined by Layer 0 |
| Layer 2 → 0, 1 | Core engine reads data from Layer 1, uses types from Layer 0. No rendering, no IO. |
| Layer 3 → 0, 1, 2 | Scripts can query data (L1), invoke engine logic (L2), and reference types (L0) |
| Layer 4 → 0, 1, 2, 3 | World engine loads maps (L1), runs scripts (L3), triggers battles (L2) |
| Layer 5 → 0, 4 | Renderer reads world state (L4) and type defs (L0). Does NOT call engine logic directly. |
| Layer 6 → 0–5 | Shell orchestrates everything. Owns the game loop and scene transitions. |
| Layer 7 → 0–6 | Mod runtime hooks into every layer via defined extension points. |
| Layer 8 → 0, 1, 5 | Editor shares data layer (L0, L1) and rendering (L5). Independent of game logic. |

## Mod Surface per Layer

| Layer | Mod Surface | How Mods Interact |
|-------|------------|-------------------|
| 0 | Extend enums/types | Mods can register new species IDs, move IDs, type IDs, item IDs. Schema is open-ended, not hardcoded to 251/251/17. |
| 1 | Add/override data files | Drop JSON into a mod folder. Data registry merges mod data over base data. Last-write-wins with explicit priority ordering. |
| 2 | Interface replacement | IDamageCalculator, IStatCalculator, ICatchCalculator, IBreedingCalculator, IAIStrategy — default implementations are Gen 2, plugins can swap them. |
| 3 | Lua scripts + new commands | Mods add Lua event scripts. Plugins can register new script commands beyond the base set. |
| 4 | New map content | Maps are data (L1). Tilesets, connections, warps, encounters are all data-driven. Mods add new maps without touching code. |
| 5 | Sprite/audio packs | Mods provide replacement or additional sprite sheets, tilesets, music, SFX. Asset pipeline loads from mod folders with override priority. |
| 6 | Menu extensions | Plugin hook for adding new start menu entries, new PC features, new shop behaviors. |
| 7 | Self-referential | Mod runtime is the extension mechanism itself. Handles load order, conflicts, dependencies. |
| 8 | n/a | Editor is a development tool, not a mod surface. But it reads/writes the same data formats mods use. |

---

## Solution Structure

Each layer maps to a .NET project in a single solution. Class libraries have no platform dependencies except where noted. The game executable targets MonoGame; the editor targets Avalonia.

| Project | Type | Description |
|---------|------|-------------|
| `PokeCrystal.Schema` | classlib | Layer 0. Types, enums, interfaces, JSON schema contracts. Zero dependencies. |
| `PokeCrystal.Data` | classlib | Layer 1. Data loaders, registry, content pipeline. Depends on Schema. |
| `PokeCrystal.Engine` | classlib | Layer 2. Battle engine, stat calc, type chart, evolution, breeding. Depends on Schema + Data. |
| `PokeCrystal.Scripting` | classlib | Layer 3. MoonSharp host, script command registry, event flag system. Depends on Schema + Data + Engine. |
| `PokeCrystal.World` | classlib | Layer 4. Map system, overworld, player, connections, time. Depends on Schema + Data + Engine + Scripting. |
| `PokeCrystal.Rendering` | classlib | Layer 5. MonoGame rendering, sprites, UI, audio. Depends on Schema + World. |
| `PokeCrystal.Game` | exe (MonoGame) | Layer 6. Game shell, scene manager, menus, save/load. References everything. |
| `PokeCrystal.Mods` | classlib | Layer 7. Mod loader, plugin host, data merger, hot-reload. References all layers. |
| `PokeCrystal.Editor` | exe (Avalonia) | Layer 8. Standalone editor. References Schema + Data + Rendering. |
| `PokeCrystal.Tests` | test project | xUnit/NUnit tests for Engine, World, Scripting. Mirrors pokered-c test strategy. |

```
PokeCrystal.sln
│
├── src/
│   ├── PokeCrystal.Schema/       ─ Layer 0: types, enums, interfaces
│   ├── PokeCrystal.Data/         ─ Layer 1: data loading, registry, content
│   ├── PokeCrystal.Engine/       ─ Layer 2: battle, stats, mechanics
│   ├── PokeCrystal.Scripting/    ─ Layer 3: Lua host, event system
│   ├── PokeCrystal.World/        ─ Layer 4: maps, overworld, player
│   ├── PokeCrystal.Rendering/    ─ Layer 5: MonoGame renderer, audio
│   ├── PokeCrystal.Game/         ─ Layer 6: game shell, menus, scenes
│   ├── PokeCrystal.Mods/         ─ Layer 7: mod runtime, plugin host
│   └── PokeCrystal.Editor/       ─ Layer 8: Avalonia editor app
├── tests/
│   └── PokeCrystal.Tests/        ─ Engine + World + Scripting tests
├── data/
│   ├── base/                     ─ Base game data (extracted from disassembly)
│   │   ├── species/              ─ JSON per species
│   │   ├── moves/                ─ JSON per move
│   │   ├── items/                ─ JSON per item
│   │   ├── types/                ─ type_chart.json
│   │   ├── maps/                 ─ Map data + tilesets + blocksets
│   │   ├── trainers/             ─ Trainer definitions
│   │   ├── encounters/           ─ Wild encounter tables per map
│   │   ├── scripts/              ─ Base Lua event scripts
│   │   └── config.json           ─ Tunable constants
│   └── mods/                     ─ Mod folders (same structure as base/)
└── tools/                        ─ Python extraction scripts from pokecrystal disassembly
```

---

## Layer 0: Data Schema & Constants

The foundation. Every type, enum, interface, and data contract lives here. This project has zero dependencies — it's pure C# with no NuGet packages, no framework references, no IO. It defines what data looks like without knowing how to load, render, or process it.

### Key Design Rules

**Open-ended IDs.** Species, moves, items, types, and abilities are identified by string keys, not integer enums. The base game defines "bulbasaur", "tackle", "potion", "normal" — mods add "fakemon_01", "custom_move", "mega_stone", "fairy" without touching the enum. Integer IDs are assigned at load time by the data registry (Layer 1) for internal array indexing, but the authoritative ID is always the string key.

**Interface contracts for engine logic.** Every replaceable mechanic gets an interface in Layer 0: IDamageCalculator, IStatCalculator, ICatchCalculator, IExperienceCalculator, IBreedingCalculator, IAIStrategy, IEvolutionEvaluator, ITypeEffectivenessResolver. Layer 2 provides default Gen 2 implementations. Layer 7 (mods) can swap them.

**Immutable data records.** Species definitions, move definitions, item definitions, and type chart entries are C# records (or readonly structs). They're loaded once and never mutated at runtime. Mutable game state (party, bag, flags, position) uses separate mutable types.

**JSON-serializable by convention.** Every data type in Layer 0 must be trivially serializable to JSON via System.Text.Json. No custom converters for basic data. This ensures the data files (Layer 1) and the editor (Layer 8) can round-trip all types without friction.

### Core Types

**SpeciesData:** base stats, types, growth rate, egg groups, gender ratio, catch rate, base EXP yield, hatch cycles, TM/HM flags, learnset, evolution entries, egg moves, sprite reference, cry reference.

**MoveData:** power, type key, accuracy, PP, effect key, priority, flags (contact, sound, punch, etc.), target mode, description.

**ItemData:** name, pocket, price, usability flags (battle/field/hold), effect key, fling power, description.

**TypeMatchup:** attacker type key, defender type key, multiplier (0 / 0.5 / 1 / 2). The full chart is a `List<TypeMatchup>` — unlisted pairs default to 1x.

**MapHeader:** id, group, dimensions, tileset reference, border block, permission, connection list, warp list, NPC list, trigger list, signpost list, script reference, encounter reference.

**TrainerData:** class, name, party list (species key, level, held item key, optional custom moves), AI strategy key, pre/post battle text references.

**PlayerState:** position (map, x, y, direction), party, bag (pocketed), money, badges, event flags, Pokédex seen/caught, registered item, phone contacts, time played.

### Interface Contracts

```csharp
public interface IDamageCalculator {
    int Calculate(BattleContext ctx, BattlePokemon attacker,
        BattlePokemon defender, MoveData move, bool isCritical);
}

public interface IStatCalculator {
    int CalcStat(SpeciesData species, int iv, int ev, int level, StatType stat);
    int CalcHp(SpeciesData species, int iv, int ev, int level);
}

public interface ITypeEffectivenessResolver {
    float GetMultiplier(string attackType, string defType1,
        string defType2, BattleContext ctx);
}

public interface IMoveEffect {
    void Apply(BattleContext ctx, BattlePokemon user,
        BattlePokemon target, MoveData move);
}

public interface IAIStrategy {
    int SelectMove(BattleContext ctx, BattlePokemon ai,
        BattlePokemon opponent, MoveData[] moves);
}
```

### Adaptable Tile Geometry (Layer 0 Schema)

Every tileset carries its own geometry definition rather than assuming 8×8 tiles and 4×4 metatiles:

```csharp
public record TilesetGeometry(
    int TileSize,           // 8, 16, or 32 — must be power of 2
    int BlockGridX,         // Tiles per block horizontally
    int BlockGridY,         // Tiles per block vertically
    string CollisionResolution  // "tile" or "block"
) {
    public int BlockPixelWidth  => TileSize * BlockGridX;
    public int BlockPixelHeight => TileSize * BlockGridY;
    public int TilesPerBlock    => BlockGridX * BlockGridY;
}
```

All downstream systems (map loader, collision, renderer, camera, scroll buffer, editor) derive tile dimensions from this geometry rather than hardcoded constants. Base Crystal ships `TileSize=8, BlockGrid=[4,4]`. A mod with 16px tiles uses `TileSize=16, BlockGrid=[2,2]` for the same 32×32 block footprint, or `BlockGrid=[1,1]` to eliminate the metatile layer entirely and paint individual tiles.

### Viewport Configuration

```csharp
public record ViewportConfig(
    int InternalWidth = 160,
    int InternalHeight = 144,
    int ScaleFactor = 3,
    ScaleMode ScaleMode = ScaleMode.IntegerNearest,
    AspectMode AspectMode = AspectMode.Fixed,
    bool AllowResize = true
);

public enum ScaleMode { IntegerNearest, NearestNeighbor, Bilinear }
public enum AspectMode { Fixed, Stretch, Expand }
```

The renderer creates a `RenderTarget2D` at `InternalWidth × InternalHeight`. All game rendering targets this surface. At display time, the render target is scaled to the window using the configured ScaleMode. Visible tile count derives from viewport dimensions: `tiles_x = InternalWidth / tile_size`.

### Graphics and Color Configuration

```csharp
public enum ColorMode { Indexed, Direct }

public record PaletteEntry(byte R, byte G, byte B);

public record Palette(string Id, PaletteEntry[] Colors);

public record PaletteSet(string Id, Dictionary<string, Palette> Palettes);

public record PaletteProfile(
    string Id,
    string Description,
    Dictionary<string, string> PaletteSetOverrides
    // Maps context keys ("morning", "day", "night", "cave") to PaletteSet IDs
);

public record SpriteSheet(
    string Id,
    string File,
    ColorMode ColorMode,
    int BitsPerPixel,       // 2, 4, 8 (indexed) or 32 (direct)
    string PaletteId,       // Indexed mode only, null for direct
    int CellWidth, int CellHeight,
    bool HasAlpha
);

public record ColorConfig(
    int MaxPalettes,        // 8 for GB-faithful, uncapped for mods
    int ColorsPerPalette,   // 4 for GB-faithful, up to 256 for extended
    int BitsPerChannel,     // 5 for GBC-faithful (15-bit), 8 for modern (24/32-bit)
    bool SupportsPaletteSwap,
    bool SupportsAlpha
);
```

Every visual asset declares its own `ColorMode`. Indexed content goes through palette lookup (preserving Crystal's palette swap effects). Direct content renders as full RGBA and gets time-of-day tinting via post-processing. Both modes coexist in the same frame — the compositor doesn't care where a pixel came from.

### Text and Font Types

```csharp
public record FontData(
    string Id, string Type,          // "bitmap" or "ttf"
    string File, string MetricsFile,
    int DefaultSize, int LineSpacing, bool IsMonospace
);

public record GlyphMetrics(
    char Character,
    int X, int Y, int Width, int Height,
    int Advance, int OffsetX, int OffsetY
);

public record TextStyle(
    string FontId, int Size, string Color,
    bool Bold, bool Italic,
    float LetterSpacing, float LineSpacingMultiplier
);

public record TextBoxStyle(
    string FrameStyle,          // "9slice", "simple_border", "none"
    string FrameSpriteSheet, int FrameTileSize,
    string BackgroundColor, float BackgroundOpacity,
    string TextColor, string TextFont, int TextSize,
    int PaddingTop, int PaddingBottom, int PaddingLeft, int PaddingRight,
    string ScrollArrowSprite, int ScrollArrowX, int ScrollArrowY
);
```

Bitmap fonts use sprite sheets with JSON glyph metrics. TTF/OTF fonts are rasterized to a glyph cache at runtime. Rich text uses inline style markers: `"{color:red}Critical hit!{/color}"`. The text engine handles per-character reveal speed, scroll-on-prompt, and text commands (player name insertion, etc.) separately from the font renderer.

### Input Mapping Types

```csharp
public enum GameAction {
    MoveUp, MoveDown, MoveLeft, MoveRight,
    Confirm, Cancel, Menu, RegisteredItem,
    SpeedToggle, QuickSave, QuickLoad, DebugConsole
}

public record InputBinding(GameAction Action, List<InputSource> Sources);

public record InputSource(InputDevice Device, string Key);

public interface IInputProvider {
    bool IsPressed(GameAction action);
    bool IsHeld(GameAction action);
    bool IsReleased(GameAction action);
    Vector2 GetAnalogDirection();
    Vector2 GetMousePosition();
    void RegisterAction(string name, GameAction action);
}
```

Game logic references `GameAction.Confirm`, never `Key.Z`. Multiple physical sources per action (keyboard + gamepad simultaneously). Mods register new actions by string key. Player rebinding saves to a separate settings file that overrides game config.

### Battle Visual Layout Types

```csharp
public record BattleLayout(
    string Id,
    BattleSlot[] PlayerSlots, BattleSlot[] EnemySlots,
    BattleHudPosition PlayerHud, BattleHudPosition EnemyHud,
    TextBoxPosition TextBox,
    MenuPosition ActionMenu, MenuPosition MoveMenu,
    string BackgroundType
);

public record BattleSlot(
    int X, int Y, float Scale, string Facing,
    int EntryOffsetX, int EntryOffsetY, float EntryDurationSec
);

public record BattleHudPosition(int X, int Y, int Width, string Alignment);
public record TextBoxPosition(int X, int Y, int Width, int Height);
public record MenuPosition(int X, int Y, int Width, int Height, int Columns);
```

Every battle element position is data. The battle engine emits events; the renderer reads the layout to decide where to draw them. A mod ships a different layout JSON for double battles, side-view battles, or any other configuration.

### UI Theming Types

```csharp
public record UITheme(
    string Id,
    TextBoxTheme TextBox, MenuTheme Menu,
    HudTheme BattleHud, CursorTheme Cursor,
    Dictionary<string, string> ColorPalette
);

public record MenuTheme(
    string FrameStyle, string FrameSpriteSheet,
    string BackgroundColor, float BackgroundOpacity,
    string TextColor, string TextFont, int TextSize,
    string SelectedColor, string SelectedBackground,
    int ItemPaddingX, int ItemPaddingY
);

public record HudTheme(
    string BackgroundColor, float BackgroundOpacity, string FrameSpriteSheet,
    string HpBarFullColor, string HpBarMidColor, string HpBarLowColor,
    string HpBarBackground, string ExpBarColor, string ExpBarBackground,
    string LevelTextColor, string NameTextColor
);

public record CursorTheme(
    string Sprite, int Width, int Height,
    bool Animated, int FrameCount, int FrameMs
);
```

Text boxes, menus, HUD, and cursors are all theme-driven. The base game ships one theme matching Crystal's look. Mods register additional themes selectable in options (like frame selection in Gen 3+). Multiple themes can be active per-context (one for menus, a different one for battle HUD).

### Audio Types

```csharp
public record MusicData(
    string Id, string Type,      // "midi", "stream", "mml"
    string File,
    float LoopStart, float LoopEnd,
    float Volume, string[] Tags
);

public record SfxData(
    string Id, string Type,      // "sample" or "synth"
    string File,                 // For sample type
    float Volume, float PitchVariance,
    SynthChannel[] Channels      // For synth type
);

public record SynthChannel(
    string Waveform, EnvelopeData Envelope,
    float Frequency, int DurationMs,
    float PitchStart, float PitchEnd, int PitchSweepMs,
    int DelayMs
);

public record EnvelopeData(int Attack, int Decay, float Sustain, int Release);

public record SoundfontConfig(
    string DefaultSoundfont,
    Dictionary<string, string> Overrides  // Context → soundfont path
);

public interface IAudioRenderer {
    void Initialize(AudioRegistry registry);
    void PlayMusic(string trackId, float fadeInSeconds = 0.5f);
    void StopMusic(float fadeOutSeconds = 0.5f);
    void CrossfadeMusic(string trackId, float durationSeconds = 1.0f);
    void PlaySfx(string sfxId);
    void PlaySfx(string sfxId, float pan, float pitch);
    void SetMusicVolume(float volume);
    void SetSfxVolume(float volume);
    void SetMasterVolume(float volume);
    void Update(float deltaTime);
    void Pause();
    void Resume();
}
```

---

## Layer 1: Data Layer

Loads, validates, and merges all game content from JSON files on disk. The central piece is the DataRegistry — a singleton that holds every loaded species, move, item, type matchup, map definition, trainer, and encounter table. It handles mod overlay: base game data loads first, then each mod's data overlays or extends it based on priority.

### Data Registry

DataRegistry is the single source of truth for all content at runtime. It maps string keys to loaded data objects and assigns internal integer indices for array-based lookups in hot paths (battle damage loops, type chart queries). It exposes both key-based and index-based access.

Loading order: (1) `base/species/*.json`, `base/moves/*.json`, etc. (2) For each mod in load order: overlay mod data. If a mod defines a species with the same key as the base, the mod's version replaces it. If the key is new, it's added. (3) After all data is loaded, assign integer indices and build lookup arrays.

### Audio Registry

Parallel to DataRegistry for game data. Manages all loaded audio assets: music tracks (MIDI or direct audio), SFX (WAV/OGG/synthesized), soundfonts (SF2/SFZ), and audio config. Supports mod overlay — a mod's audio files override or extend the base set by key.

Music data example:

```json
{
  "id": "new_bark_town",
  "type": "midi",
  "file": "music/new_bark_town.mid",
  "loop_start": 4.2,
  "loop_end": 48.0,
  "volume": 0.85,
  "tags": ["town", "johto", "peaceful"]
}
```

SFX data (sample-based and synthesized):

```json
{
  "id": "sfx_damage_normal",
  "type": "sample",
  "file": "sfx/damage_normal.wav",
  "volume": 0.9,
  "pitch_variance": 0.05
}
```

```json
{
  "id": "sfx_damage_normal_retro",
  "type": "synth",
  "channels": [{
    "waveform": "noise",
    "envelope": { "attack": 0, "decay": 80, "sustain": 0, "release": 40 },
    "frequency": 440,
    "duration_ms": 120
  }]
}
```

### Content Pipeline

Tilesets and sprites are stored as PNG sprite sheets with accompanying JSON metadata (tile dimensions, animation frames, palette info, color mode). The content pipeline converts these into runtime texture atlases, respecting each asset's declared ColorMode (indexed or direct). Maps reference tilesets by key; the pipeline resolves keys to loaded atlas regions.

Audio content: MIDI files parsed via DryWetMIDI, SF2/SFZ soundfonts loaded via NFluidsynth or NAudio, direct audio (OGG/WAV/MP3) decoded for streaming. Pokémon cries are pre-rendered WAV files keyed by species, or synthesized from parameter definitions via the RetroSynth component.

### Extraction Tooling

Python scripts in `tools/` parse the pokecrystal disassembly and output the `base/` data files. This is the one-time conversion from ASM to JSON. Scripts cover: species base stats, move data, item data, type chart, map headers, map tile/block data, wild encounters, trainer parties, tileset graphics (as indexed PNGs), sprite graphics, and music sequences (converted to MIDI with a custom GB APU SF2 soundfont).

The Crystal music conversion pipeline: parse `audio/` ASM → map 4 GB channels to MIDI channels (square→ch0/1 with duty-based program numbers, wave→ch2, noise→ch9 percussion) → convert note events with velocity from volume envelope → embed loop points in MusicData JSON → output `.mid` files and the GB APU `.sf2` soundfont.

---

## Layer 2: Core Engine

Pure game logic with no rendering, no IO, no MonoGame references. This is testable in isolation. Every formula, every mechanic, every rule from Gen 2 lives here behind the interfaces defined in Layer 0.

### Battle Engine

The battle engine is a stateful object (BattleContext) that processes turns. It does not render anything — it emits a sequence of BattleEvents (damage dealt, status applied, stat changed, fainted, weather changed, item consumed, etc.) that the UI layer (Layer 5/6) consumes to drive animations and text.

This event-driven design is the key architectural difference from pokered-c. In the C port, battle logic and UI were interleaved via state machine. Here, logic and presentation are fully separated. A battle can run headless (for AI training, automated testing, or Showdown-style simulators) or be visualized through any frontend.

Default implementations: Gen2DamageCalculator, Gen2StatCalculator, Gen2CatchCalculator, Gen2ExperienceCalculator, Gen2BreedingCalculator, Gen2TypeResolver. All registered with a service container so mods can replace them.

### Move Effect System

Each move effect is a class implementing IMoveEffect, registered by effect key. Crystal has ~100 unique effects. The base engine ships implementations for all of them. Mods register new effect classes; the data layer references them by key in move JSON files.

Effect resolution: `MoveData.EffectKey → registry lookup → IMoveEffect instance → Apply()`. This replaces the hardcoded switch/dispatch from pokered-c with an open registry that mods extend without touching engine code.

### AI System

Trainer AI strategies are classes implementing IAIStrategy. The base engine provides: RandomAI (wild encounters), BasicAI (most trainers), SmartAI (gym leaders, Elite Four). Each evaluates available moves and scores them based on type effectiveness, damage potential, status utility, and HP thresholds. TrainerData references an AI strategy by key.

---

## Layer 3: Scripting & Events

MoonSharp (Lua interpreter for .NET) hosts all event scripts. The engine exposes a curated API surface to Lua — scripts can read game state, set/check event flags, display text, give items/Pokémon, move NPCs, trigger battles, warp the player, and play sound effects. Scripts cannot directly mutate engine internals.

### Script Command API

The Lua API mirrors Crystal's original script command set but with cleaner naming. Base commands include: `msg(text)`, `yes_no()`, `give_item(key, count)`, `give_pokemon(key, level)`, `check_flag(name)`, `set_flag(name)`, `warp(map, x, y)`, `face_player()`, `move_npc(id, path)`, `play_sound(key)`, `play_music(id)`, `crossfade_music(id, seconds)`, `heal_party()`, `open_mart(inventory)`, `battle_trainer(key)`.

Each command is a C# class implementing `IScriptCommand`, registered by name with the script runtime. The base engine registers ~40 commands. Mods register new ones via plugin DLLs. The runtime doesn't distinguish between base and mod commands — they're all entries in the same name→handler registry.

The Lua runtime itself is replaceable via an `IScriptRuntime` interface. MoonSharp is the default; a mod could swap in Roslyn, Jint, or IronPython if desired. Scripts are scoped per-map but can call shared functions from a global library. Mods ship `lib/` folders with utility functions their map scripts import. Mods can override base library functions to change common patterns globally.

### Event Flag System

A dictionary of string-keyed boolean flags (not a bit array — mod-friendly). Base game flags match Crystal's event constants. Mods add their own flags in their own namespace (e.g., "mymod.defeated_custom_trainer"). Flags are persisted in save data as a flat key-value list.

---

## Layer 4: World Engine

The overworld simulation. Loads maps from the data layer, manages the player's position and movement, handles map connections and warps, runs NPC movement, checks collision, triggers encounters, and ticks the time-of-day system. This layer owns the overworld game state but does not render it.

### Map System

Maps are loaded from data as MapHeader + block data + tileset reference. The block/metatile system reads its geometry from the tileset's `TilesetGeometry` definition — block size, tile size, and collision resolution are all data, never hardcoded constants.

Connections between maps are explicit data (direction, target map key, alignment offset). Map connections between different tilesets are allowed only if block pixel dimensions match on the connected edge. The map system handles an arbitrary number of maps and map groups — not hardcoded to Crystal's 26 groups.

Scroll buffer sizing derives from tileset geometry: `scroll_buffer_x = screen_tiles_x + (2 * block_grid_x)`, `scroll_buffer_y = screen_tiles_y + (2 * block_grid_y)`. Player movement step size is configurable per tileset rather than derived purely from tile size.

### Time System

Time-of-day (morning/day/night) drives encounter tables, palette shifts (via PaletteProfile), NPC availability, and evolution conditions. The time source is abstracted behind an `ITimeProvider` interface — the default uses real-time, but mods or debug tools can inject fixed or accelerated time. Time periods are configurable (a mod could add "dusk" or change the hour boundaries).

### Overworld Loop

Per-frame tick: process input (via `IInputProvider` actions) → update player movement → update NPC movement → check warps → check connections → check triggers → check encounters → run active scripts. Matches Crystal's OverworldLoop structure but non-blocking by design. The transient state cleanup rules from pokered-c apply: every flag is reset on state transitions.

---

## Layer 5: Rendering & Audio

MonoGame rendering pipeline. Reads world state from Layer 4, battle state from Layer 2 (via events), and UI state from Layer 6 to composite the final frame. This layer is the only one that imports MonoGame.Framework.

### Graphics Pipeline

The renderer supports two color paths that coexist in every frame:

**Indexed path:** Read pixel data from tile/sprite (2bpp/4bpp index) → look up current palette via `IPaletteManager` → map index to RGBA → write to framebuffer. Palette swaps (time-of-day, flash, fade, cave darkness, poison pulse) work by changing the active palette — pixel data never changes.

**Direct color path:** Read RGBA pixel data → write directly to framebuffer with alpha compositing. Time-of-day effects applied as post-processing tint rather than palette swap.

Compositing order: background tiles → sprites → overlay tiles (tall grass, bridges) → post-process tint (world layers only) → UI layer (no tint, always readable) → scale to window via ViewportConfig.

The palette manager handles runtime palette operations:

```csharp
public interface IPaletteManager {
    void LoadPaletteSet(string paletteSetId);
    void SetPalette(string slotName, Palette palette);
    void FadeToBlack(float durationSec);
    void FadeToWhite(float durationSec);
    void CrossfadePaletteSet(string targetSetId, float durationSec);
    void SetWorldTint(PaletteEntry color, float intensity);
    void FadeWorldTint(PaletteEntry targetColor, float targetIntensity, float durationSec);
}
```

Time-of-day tint config:

```json
{
  "time_tints": {
    "morning": { "color": "#FFD080", "intensity": 0.15 },
    "day": { "color": "#FFFFFF", "intensity": 0.0 },
    "night": { "color": "#4060A0", "intensity": 0.3 }
  }
}
```

### Tile Renderer

Renders the overworld from block/tile data, reading geometry from the active tileset. No `const TILE_SIZE = 8` anywhere — every pixel calculation goes through the tileset's `TilesetGeometry`. Camera follows player with connection-aware scrolling. Indoor maps clamp camera to map bounds.

The renderer operates on abstract tile/sprite data from Layer 0/1 — it doesn't know about Pokémon mechanics. The editor (Layer 8) reuses the exact same rendering code.

### Battle Renderer

Consumes BattleEvents from Layer 2 and renders them using the active `BattleLayout` definition. Sprite positions, HUD locations, text box placement, and entry animations are all read from layout data. The renderer maps events to visual slots based on the layout — it doesn't know about "top-right" or "bottom-left."

Battle backgrounds are data-driven:

```json
{
  "battle_backgrounds": {
    "wild_grass": { "file": "battle/bg_grass.png", "animated": false },
    "water": { "file": "battle/bg_water.png", "animated": true, "frame_count": 4, "frame_ms": 200 }
  }
}
```

### Audio System

Three playback modes that coexist:

**Sequenced (MIDI + Soundfont):** Music data → Sequencer → Synthesizer (with loaded SF2/SFZ) → Mixer → Output. The same MIDI file sounds different depending on the soundfont. Base game ships a custom GB APU SF2; swap to General MIDI for "enhanced," or a custom orchestral SF2 for cinematic mods.

**Streamed (Direct Audio):** Audio file → StreamPlayer → Mixer → Output. Bypasses sequencing. For studio-produced music, voice acting, ambient soundscapes.

**Hybrid:** Some channels sequenced, some streamed — the mixer doesn't care where input comes from.

The base game soundfont maps MIDI programs to GB APU channels: programs 0–3 = square wave at 12.5/25/50/75% duty, program 4 = programmable wave, percussion bank = noise channel. The extraction tooling generates both the MIDI files and this SF2 from Crystal's audio data.

MonoGame integration: SFX → `SoundEffectInstance` pool, streamed music → `Song`, sequenced music → `DynamicSoundEffectInstance` buffer filled by the synthesizer on a background thread.

Pokémon cries: pre-rendered WAV files keyed by species, or synthesized from JSON parameters via the RetroSynth component. Mods add cries for new species by including WAV files or synth definitions.

Audio config:

```json
{
  "audio": {
    "master_volume": 1.0,
    "music_volume": 0.8,
    "sfx_volume": 1.0,
    "sample_rate": 44100,
    "buffer_size": 2048,
    "max_concurrent_sfx": 8,
    "sfx_suspends_music_channel": false,
    "crossfade_default_seconds": 0.5,
    "enable_retro_synth": true
  }
}
```

NuGet dependencies: DryWetMIDI (MIDI parsing), NFluidsynth or NAudio (SF2 rendering), NVorbis (OGG decoding if needed).

### Text Renderer

Accepts styled text spans and a target rectangle. Lays out glyphs using the loaded font's metrics, handles word wrapping, and renders glyph-by-glyph. Bitmap fonts blit from the sprite sheet; TTF fonts rasterize to a texture cache. Rich text markers are parsed into styled spans: `"{style:damage}Critical hit!{/style}"` applies the named style from the active theme.

### UI Framework

Text boxes, menus, selection lists, HP bars, and other UI elements rendered using the active `UITheme`. 9-slice frame rendering for text boxes and menus. The framework is generic — it doesn't know about Pokémon, just "render a menu with N options in this theme." Supports custom fonts, cursor sprites, and color palettes from mod theme data.

---

## Layer 6: Game Shell

The application layer. Owns the MonoGame Game class, the main loop, scene transitions, and all menu systems.

### Scene Manager

Scenes: TitleScreen, NewGame, Overworld, Battle, Evolution, Trading, HallOfFame, Credits. Each scene owns its update/draw cycle. Transitions between scenes use fade effects (via `IPaletteManager`). The overworld scene delegates to Layer 4 for logic and Layer 5 for rendering.

### Menu Systems

Start menu with submenus: Pokédex, Party, Bag, Player Card, Save, Options. Each submenu is a self-contained UI state machine using the active `UITheme` and responding to `GameAction` inputs (never raw keys). Party screen supports reordering, item use, move inspection, and field move activation. Bag screen has pocketed tabs. Mart screen has buy/sell with money validation. PC screen has deposit/withdraw/release/move.

Menus are extensible via a hook system — mods register additional start menu entries or PC features through the plugin API.

Options menu includes: text speed selection, battle animation toggle, battle style, input rebinding, UI theme selection, audio volume sliders.

### Save System

Serializes PlayerState + event flags + box data to JSON (not binary). Save files are human-readable, mod-friendly, and forward-compatible — unknown fields from newer mods are preserved on load even if the mod isn't active. Checksum validation for integrity. Multiple save slots.

---

## Layer 7: Mod Runtime

The mod system coordinates all three tiers of modding: data files, Lua scripts, and compiled C# plugins.

### Loading Pipeline

On startup: (1) Discover mods in `data/mods/` — each mod is a folder with a `manifest.json` declaring name, version, author, dependencies, load priority, and compatibility. (2) Resolve load order from dependencies and priorities. (3) For each mod in order: merge data files into the DataRegistry (Layer 1), register Lua scripts with the script runtime (Layer 3), load DLL plugins and call their registration entry points.

### Data Mod Tier

The simplest mod type. A mod folder contains JSON files mirroring the `base/` structure. Any file present in the mod overlays the corresponding base file. New files add new content. This covers: new species, moves, items, types, maps, encounters, trainers, tilesets, soundfonts, audio, UI themes, battle layouts, and fonts. No code required.

### Script Mod Tier

Lua scripts in the mod's `scripts/` folder. These define event handlers, NPC behaviors, and custom interactions using the script command API (Layer 3). Scripts run in a sandboxed MoonSharp environment with access only to the exposed API — no filesystem, no network, no reflection.

### Plugin Mod Tier

Compiled C# DLLs in the mod's `plugins/` folder. Plugins implement `IModPlugin` and are loaded via reflection. They can: register new IMoveEffect implementations, replace IDamageCalculator or other engine interfaces, add new script commands, register menu extensions, hook into lifecycle events (on battle start, on map load, on save, etc.), and register custom `IAudioRenderer` or `IPostProcessor` implementations.

### Hot Reload

Data files and Lua scripts support hot reload during development — change a JSON file or script, and the engine picks up the change without restarting. DLL plugins require a restart. The editor (Layer 8) triggers hot reload automatically when saving.

---

## Layer 8: Editor (Standalone)

An Avalonia desktop application for creating and editing game content. It shares PokeCrystal.Schema, PokeCrystal.Data, and PokeCrystal.Rendering as project references — it loads the same data files the game uses and renders maps with the same tile renderer. Edits save directly to the `data/` folder; the game hot-reloads them.

### Map Editor

Visual block-painting on a grid. The grid cell size adapts to the active tileset's `TilesetGeometry` — not hardcoded to 32×32. Tileset palette panel shows available blocks with collision overlays. Block editor lets you compose metatiles from individual tiles with per-tile collision and overlay flags, adapting to `BlockGridX × BlockGridY`. Map properties panel for dimensions, tileset assignment, border block, and permission. Connection editor: drag maps adjacent to define connections with visual alignment. Warp editor: click to place warps, set destination by picking from a map list. Undo/redo with full history.

### Event Editor

Place NPCs, signs, coordinate triggers, and hidden items on the map visually. Each object has a property panel for sprite, movement type, script reference, and conditional visibility (event flag gates). Script editor panel with Lua syntax highlighting, autocompletion for script commands, and inline documentation.

### Encounter Editor

Per-map, per-time-of-day encounter table editor. Species picker with search/filter, level range sliders, encounter rate controls. Visual probability distribution preview showing how likely each species is.

### Tileset Editor

Import PNG sprite sheets at any supported tile size (8/16/32). Define tile boundaries, assign collision types per tile, compose metatiles/blocks by dragging tiles into a grid that matches the tileset's `BlockGridX × BlockGridY`. Preview blocks at multiple zoom levels. Animated tile support with frame timing controls. Color mode indicator (indexed vs direct). Export to the engine's tileset format.

### Live Preview

An embedded MonoGame render surface (via Avalonia's native interop) showing the map as it appears in-game. Player position preview, time-of-day toggle (with palette swap or tint depending on tileset color mode), NPC visibility. Walk-test mode lets you navigate the map in the editor to verify collision and warps without launching the full game.

---

## Implementation Order

Build in layer order, but within each layer prioritize what unblocks the next layer. Each phase should end with a testable milestone.

### Phase 1: Foundation (Layers 0–1)

1. **`PokeCrystal.Schema`** — All types, enums, interfaces, including tile geometry, viewport config, color types, font types, input types, battle layout types, UI theme types, and audio types. The contract for everything above.
2. **Extraction tooling** — Python scripts that parse pokecrystal-master and output `base/` JSON, including MIDI conversion and GB APU SF2 soundfont generation.
3. **`PokeCrystal.Data`** — DataRegistry, AudioRegistry, JSON loaders, content pipeline stubs.

*Milestone: Load all 251 species, all moves, the type chart, and a handful of maps from JSON. Query them by key. Load a soundfont and parse a MIDI file.*

### Phase 2: Core Engine (Layer 2)

4. **Stat calculator + type resolver** — The two most-called engine systems.
5. **Damage calculator + move effects** — Port the battle formulas using pokered-c lessons.
6. **Full battle engine** — Turn loop, AI, items, switching, catch, EXP, evolution.

*Milestone: Run a headless battle between two trainers, verify damage values match Crystal.*

### Phase 3: World (Layers 3–4)

7. **Map loading + overworld** — Load maps with geometry-aware tile rendering, render a placeholder grid, move the player.
8. **Connections + warps + collision** — Full navigation between maps.
9. **Scripting runtime** — MoonSharp integration, base command set, event flags.
10. **NPC system + encounters** — NPCs, interactions, wild encounter triggers.

*Milestone: Walk between Johto maps, talk to NPCs, enter wild battles.*

### Phase 4: Presentation (Layers 5–6)

11. **Tile renderer + sprites** — MonoGame rendering with dual color mode support (indexed + direct), palette manager, viewport scaling.
12. **UI framework + menus** — Theme-driven text boxes, start menu, party screen, bag, PC, with font system and input mapping.
13. **Audio** — Sequencer, synthesizer, mixer, SFX, direct audio streaming, soundfont loading.
14. **Scene flow** — Title screen, new game, save/load, hall of fame.

*Milestone: Playable game from title screen through the first gym.*

### Phase 5: Modding + Editor (Layers 7–8)

15. **Mod loader + data overlay** — Load mods, merge data files, merge audio.
16. **Plugin host** — DLL loading, interface replacement, lifecycle hooks.
17. **Editor shell** — Avalonia app with geometry-adaptive map editor and tileset editor.
18. **Event + encounter editors** — NPC placement, script editing, encounter tables.
19. **Live preview** — Embedded MonoGame renderer in the editor with palette/tint preview.

*Milestone: Create a custom map in the editor, add it as a mod, play it in the game.*

---

*pokecrystal-cs Complete Architecture Specification — Seed into monet-code before starting implementation.*
