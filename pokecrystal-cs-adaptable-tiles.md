# Adaptable Tile System — Architecture Addendum

## Where This Fits

This work slots between **Phase 1 (Layers 0–1)** and **Phase 2 (Layer 2)** in the implementation order. Specifically, it's the last thing you do in Phase 1 before moving to the engine layer.

The tile geometry parameters are Layer 0 schema definitions. The tileset loader that reads them is Layer 1. Both must be in place before Layer 4 (World) builds the map system on top of them, and before Layer 5 (Rendering) writes the tile renderer. If the geometry is hardcoded in Layers 4/5 and you try to parameterize it after the fact, you're refactoring everything that touches tiles.

**Implementation moment:** After extraction tooling outputs the base game JSON (Phase 1, step 2) and before the DataRegistry is finalized (Phase 1, step 3). The tileset JSON schema must include geometry fields from day one so the registry, the world engine, the renderer, and the editor all build against the parameterized model — never against constants.

---

## The Problem

Crystal's tile system is three nested grids with fixed dimensions:

- **Tile:** 8×8 pixels — the atomic unit of graphics
- **Block (metatile):** 4×4 tiles = 32×32 pixels — the atomic unit of map painting
- **Map grid:** N×M blocks — the map dimensions

Every system in the original game hardcodes these sizes: the renderer assumes 8px tiles, the collision system assumes 32px blocks, the camera assumes a 160×144 viewport that's 20×18 tiles, the map connection alignment is in block units, and the editor tools in the ROM hacking community all bake in the same assumptions.

For a moddable engine this is unnecessarily restrictive. A mod using hand-painted 16×16 tiles shouldn't have to pack them into 8×8 sub-tiles. A mod going for a higher-resolution look shouldn't have to quadruple its tile count to fill the same screen space.

---

## The Design

### Geometry Parameters

Every tileset carries its own geometry definition:

```json
{
  "id": "johto_outdoor",
  "tile_size": 8,
  "block_grid": [4, 4],
  "collision_resolution": "tile",
  "sprite_sheet": "tilesets/johto_outdoor.png",
  "blocks": "tilesets/johto_outdoor_blocks.json"
}
```

Three parameters control the entire pipeline:

**`tile_size`** — Pixel dimensions of one tile (always square). Must be a power of 2. Valid values: 8, 16, 32. The base game uses 8.

**`block_grid`** — How many tiles compose one block/metatile, as `[columns, rows]`. The base game uses `[4, 4]`. A mod with 16px tiles might use `[2, 2]` to keep 32×32 blocks, or `[4, 4]` for 64×64 blocks. Setting `[1, 1]` effectively eliminates the metatile layer — each "block" is a single tile, and the map painter works at tile granularity.

**`collision_resolution`** — Whether collision is checked per-tile or per-block. `"tile"` means each tile in a block can have independent collision. `"block"` means the entire block is passable or impassable as a unit. The base game effectively uses per-tile (the collision list is tile IDs within blocks).

### Derived Constants

Everything else is calculated, never hardcoded:

```
block_pixel_width  = tile_size * block_grid[0]
block_pixel_height = tile_size * block_grid[1]
tiles_per_block    = block_grid[0] * block_grid[1]
screen_tiles_x     = viewport_width  / tile_size
screen_tiles_y     = viewport_height / tile_size
screen_blocks_x    = viewport_width  / block_pixel_width
screen_blocks_y    = viewport_height / block_pixel_height
scroll_buffer_x    = screen_tiles_x + (2 * block_grid[0])
scroll_buffer_y    = screen_tiles_y + (2 * block_grid[1])
```

The viewport dimensions themselves are also configurable (see below), but default to 160×144 for base Crystal fidelity.

### Viewport Configuration

The viewport is defined in the game config, not in the tileset:

```json
{
  "viewport": {
    "width": 160,
    "height": 144,
    "scale": 3
  }
}
```

A mod could set `"width": 320, "height": 288` for a 2× resolution viewport while keeping 8px tiles (40×36 tiles visible instead of 20×18). Or use 16px tiles at 320×288 for the same 20×18 tile grid but at higher graphical fidelity. The renderer scales the final framebuffer by `scale` for display.

---

## What Each Layer Needs to Do

### Layer 0 (Schema)

Define the `TilesetGeometry` type:

```csharp
public record TilesetGeometry(
    int TileSize,
    int BlockGridX,
    int BlockGridY,
    string CollisionResolution  // "tile" or "block"
) {
    public int BlockPixelWidth  => TileSize * BlockGridX;
    public int BlockPixelHeight => TileSize * BlockGridY;
    public int TilesPerBlock    => BlockGridX * BlockGridY;
}
```

Define `ViewportConfig`:

```csharp
public record ViewportConfig(
    int Width = 160,
    int Height = 144,
    int Scale = 3
);
```

Add `TilesetGeometry Geometry` as a required field on the `TilesetData` type. No tileset can be loaded without declaring its geometry.

### Layer 1 (Data)

The tileset loader parses geometry from JSON and validates constraints (tile_size is power of 2, block_grid values are positive). The content pipeline uses geometry to correctly slice the sprite sheet into tiles — it reads tile_size to know the grid spacing, not a hardcoded 8.

The block definition loader reads `block_grid` to know how many tile indices per block entry. A `[4, 4]` block has 16 tile indices; a `[2, 2]` block has 4.

When a map references a tileset, the DataRegistry makes the geometry available to every system that needs it.

### Layer 4 (World)

The map loader uses the tileset's geometry to interpret block data. Map dimensions are always in block units, but the pixel size of those blocks depends on the tileset.

The collision system reads `collision_resolution` to decide whether to test per-tile or per-block. The collision lookup table is indexed accordingly.

Map connections use block-unit alignment offsets. Since blocks can be different sizes in different tilesets, connections between maps with different tilesets need validation — the connection alignment math must account for potentially different block pixel sizes on each side. For the base game this is a non-issue (all Crystal maps use 8px/4×4), but the system should either enforce matching geometry at connections or explicitly handle the mismatch.

Scroll buffer sizing uses the derived constants from geometry, not hardcoded values. The pokered-c lesson about needing 2 extra tiles per edge becomes 2 extra *block_grid units* per edge.

Player movement step size is derived from tile_size. The base game moves 16px per step (2 tiles). With 16px tiles, a step might be 1 tile (16px) or 2 tiles (32px) depending on the desired feel — this should be a configurable movement parameter, not derived purely from tile size.

### Layer 5 (Rendering)

The tile renderer reads geometry from the active map's tileset to determine:
- Tile source rectangles on the sprite sheet atlas (tile_size × tile_size regions)
- Block composition (which tiles at which offsets within a block)
- Scroll buffer dimensions
- Camera-to-tile coordinate conversion

No `const int TILE_SIZE = 8` anywhere in the renderer. Every pixel calculation goes through the geometry.

Sprite rendering is independent of tile geometry — sprites have their own dimensions and are positioned in pixel coordinates. But the sprite-to-tile overlap logic (tall grass overlay, bridge layering) needs to account for variable tile sizes.

### Layer 8 (Editor)

The map editor reads the active tileset's geometry to configure:
- The block palette panel (blocks rendered at their actual pixel dimensions)
- The metatile/block composer (grid adapts to block_grid dimensions)
- The map painting grid (cell size matches block pixel size)
- The collision overlay (per-tile or per-block based on collision_resolution)
- Zoom levels (based on tile_size to keep the UI usable at all scales)

The tileset editor validates that imported sprite sheets have dimensions divisible by tile_size.

---

## Example Configurations

### Base Crystal (default)
```json
{
  "tile_size": 8,
  "block_grid": [4, 4],
  "collision_resolution": "tile"
}
```
8px tiles, 32×32 blocks, 20×18 tile viewport at 160×144. Pixel-perfect Crystal.

### HD Tiles, Same Block Size
```json
{
  "tile_size": 16,
  "block_grid": [2, 2],
  "collision_resolution": "tile"
}
```
16px tiles, still 32×32 blocks. Same map scale, sharper graphics. Block definitions have 4 tile indices instead of 16, so maps store the same amount of data but tiles carry more visual detail.

### HD Tiles, Larger Blocks
```json
{
  "tile_size": 16,
  "block_grid": [4, 4],
  "collision_resolution": "tile"
}
```
16px tiles, 64×64 blocks. Each metatile is a rich 64×64 pixel scene. Map grids are coarser but each block carries 4× the visual information. Good for detailed terrain.

### Direct Tile Painting (No Metatile Layer)
```json
{
  "tile_size": 16,
  "block_grid": [1, 1],
  "collision_resolution": "tile"
}
```
Each "block" is a single 16×16 tile. The map grid is the tile grid — no metatile abstraction. Map painter works like Tiled or RPG Maker. Simplest mental model for modders, largest map data files (every tile stored individually instead of block references).

### Hi-Res Widescreen
```json
{
  "tile_size": 16,
  "block_grid": [2, 2],
  "collision_resolution": "tile"
}
```
With viewport `{"width": 320, "height": 240}`: 20×15 tiles visible, each 16px. Widescreen aspect ratio, same visual density as base Crystal but at 2× resolution. Could work well with the GBA-inspired widescreen viewport dimensions explored in other projects (284×160 scaled up).

---

## Constraints and Validation

- `tile_size` must be 8, 16, or 32. Enforced at load time.
- `block_grid` values must be positive integers, max 8×8 (to prevent absurdly large blocks).
- Sprite sheet dimensions must be divisible by `tile_size`.
- Block definition arrays must have exactly `block_grid[0] * block_grid[1]` tile indices per block.
- All maps using the same tileset share its geometry. Mixed geometries on a single map are not supported.
- Map connections between different tilesets are allowed only if block pixel dimensions match on the connected edge, or the engine pads/clips the connection strip to align. The base game never hits this case, so it's a mod-only concern that can be validated in the editor.

---

## What This Does NOT Cover

- **Sprite dimensions.** Player and NPC sprites have their own size independent of tile geometry. A 16×32 player sprite works the same on 8px tiles and 16px tiles — it's positioned in pixel coordinates.
- **Battle scene layout.** Battle rendering has no tile grid — it's a fixed UI layout. Tile geometry is purely an overworld/map concern.
- **UI tile rendering.** Text boxes and menus use their own tile/font system that's independent of map tilesets.
