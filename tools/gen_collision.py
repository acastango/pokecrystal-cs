"""
Converts Crystal .blk files + tileset collision ASM into JSON collision arrays.
Usage: python tools/gen_collision.py
Outputs updated collision arrays for each map in data/base/maps/*.json
"""

import re, struct, json, os

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# -----------------------------------------------------------------------
# COLL_* name → byte value (from constants/collision_constants.asm)
# -----------------------------------------------------------------------
COLL = {
    "FLOOR": 0x00, "01": 0x01, "03": 0x03, "04": 0x04,
    "WALL": 0x07,
    "CUT_TREE": 0x12, "LONG_GRASS": 0x14, "HEADBUTT_TREE": 0x15,
    "TALL_GRASS": 0x18,
    "WATER": 0x29, "WHIRLPOOL": 0x24, "BUOY": 0x27,
    "WATERFALL": 0x33,
    "ICE": 0x23,
    "WARP_CARPET_DOWN": 0x70, "DOOR": 0x71, "LADDER": 0x72,
    "STAIRCASE": 0x7A, "CAVE": 0x7B, "WARP_PANEL": 0x7C,
    "WARP_CARPET_LEFT": 0x76, "WARP_CARPET_UP": 0x78, "WARP_CARPET_RIGHT": 0x7E,
    "COUNTER": 0x90, "BOOKSHELF": 0x91, "PC": 0x93,
    "HOP_RIGHT": 0xA0, "HOP_LEFT": 0xA1, "HOP_DOWN": 0xA3,
    "HOP_DOWN_RIGHT": 0xA4, "HOP_DOWN_LEFT": 0xA5,
    "UP_WALL": 0xB2, "RIGHT_WALL": 0xB0, "LEFT_WALL": 0xB1, "DOWN_WALL": 0xB3,
}

def parse_tilecoll(asm_path):
    """Parse a *_collision.asm → list of (nw, ne, sw, se) COLL_* tuples, one per metatile."""
    table = []
    pat = re.compile(r'tilecoll\s+(\w+)\s*,\s*(\w+)\s*,\s*(\w+)\s*,\s*(\w+)')
    with open(asm_path) as f:
        for line in f:
            m = pat.search(line)
            if m:
                quads = tuple(COLL.get(m.group(i), 0x00) for i in range(1, 5))
                table.append(quads)
    return table

def load_blk(blk_path):
    with open(blk_path, "rb") as f:
        return list(f.read())

def blk_to_collision(blk_data, blk_w, blk_h, tilecoll_table):
    """
    Expand (blk_w × blk_h) blocks → (blk_w*2 × blk_h*2) tile collision array.
    Each block → 4 tiles: NW, NE, SW, SE (row-major in tile coords).
    """
    tw = blk_w * 2  # tile width
    th = blk_h * 2  # tile height
    out = [0] * (tw * th)
    for by in range(blk_h):
        for bx in range(blk_w):
            mt = blk_data[by * blk_w + bx]
            if mt < len(tilecoll_table):
                nw, ne, sw, se = tilecoll_table[mt]
            else:
                nw = ne = sw = se = 0x07  # unknown metatile → wall
            tx, ty = bx * 2, by * 2
            out[ty       * tw + tx    ] = nw
            out[ty       * tw + tx + 1] = ne
            out[(ty + 1) * tw + tx    ] = sw
            out[(ty + 1) * tw + tx + 1] = se
    return out, tw, th

# -----------------------------------------------------------------------
# Map definitions: (json_id, blk_filename, tilecoll_asm, blk_w, blk_h)
# -----------------------------------------------------------------------
MAPS = [
    ("ROUTE_29",        "Route29.blk",        "johto_collision.asm",  30, 9),
    ("NEW_BARK_TOWN",   "NewBarkTown.blk",    "johto_collision.asm",  10, 9),
    ("CHERRYGROVE_CITY","CherrygroveCity.blk","johto_collision.asm",  20, 9),
    ("ROUTE_46",        "Route46.blk",        "johto_collision.asm",  10, 18),
    ("ROUTE_30",        "Route30.blk",        "johto_collision.asm",  10, 18),
]

def update_json(map_id, tileset_id, blocks, blk_w, blk_h, collision, tw, th):
    path = os.path.join(REPO, "data", "base", "maps", f"{map_id}.json")
    with open(path) as f:
        data = json.load(f)
    data["width"]      = tw
    data["height"]     = th
    data["tilesetId"]  = tileset_id
    data["blkWidth"]   = blk_w
    data["blkHeight"]  = blk_h
    data["blocks"]     = blocks
    data["collision"]  = collision
    with open(path, "w") as f:
        json.dump(data, f, indent=2)
    print(f"  {map_id}: {blk_w}x{blk_h} blocks ({tw}x{th} tiles), tileset={tileset_id}")

def main():
    blk_dir   = os.path.join(REPO, "pokecrystal-master", "maps")
    coll_dir  = os.path.join(REPO, "pokecrystal-master", "data", "tilesets")
    tilecoll_cache = {}

    print("Generating collision + block data from Crystal source...")
    for map_id, blk_file, asm_file, blk_w, blk_h in MAPS:
        blk_path = os.path.join(blk_dir, blk_file)
        asm_path = os.path.join(coll_dir, asm_file)
        if not os.path.exists(blk_path):
            print(f"  SKIP {map_id}: {blk_path} not found")
            continue
        if asm_file not in tilecoll_cache:
            tilecoll_cache[asm_file] = parse_tilecoll(asm_path)
        table = tilecoll_cache[asm_file]
        blk = load_blk(blk_path)
        blocks = blk[:blk_w * blk_h]  # raw metatile indices, row-major
        collision, tw, th = blk_to_collision(blk, blk_w, blk_h, table)
        # Tileset name = asm file prefix (johto_collision.asm → "johto")
        tileset_id = asm_file.replace("_collision.asm", "")
        update_json(map_id, tileset_id, blocks, blk_w, blk_h, collision, tw, th)
    print("Done.")

if __name__ == "__main__":
    main()
