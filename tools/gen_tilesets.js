/**
 * gen_tilesets.js — Extract Crystal tileset and sprite palette data for use at runtime.
 *
 * Reads:
 *   pokecrystal-master/gfx/tilesets/bg_tiles.pal
 *   pokecrystal-master/gfx/tilesets/{tileset}_palette_map.asm
 *   pokecrystal-master/gfx/overworld/npc_sprites.pal
 *
 * Writes:
 *   data/tilesets/bg_palettes.json            — day-time RGBA for each BG palette name
 *   data/tilesets/{tileset}_tile_palettes.bin — one byte per tile = palette index 0-7
 *   data/sprites/npc_sprite_palettes.json     — day-time RGBA for each NPC palette name
 *
 * Usage:
 *   node tools/gen_tilesets.js
 */

const fs   = require('fs');
const path = require('path');

const ROOT         = path.join(__dirname, '..');
const GFX_PAL      = path.join(ROOT, 'pokecrystal-master', 'gfx', 'tilesets', 'bg_tiles.pal');
const NPC_PAL      = path.join(ROOT, 'pokecrystal-master', 'gfx', 'overworld', 'npc_sprites.pal');
const PAL_MAP      = path.join(ROOT, 'pokecrystal-master', 'gfx', 'tilesets');
const OUT_DIR      = path.join(ROOT, 'data', 'tilesets');
const SPRITES_DIR  = path.join(ROOT, 'data', 'sprites');

// ── Parse bg_tiles.pal ────────────────────────────────────────────────────────
// Format: section header ("; morn", "; day" …) then
//   RGB r,g,b, r,g,b, r,g,b, r,g,b ; name
// All values are 5-bit (0-31); multiply by 8 to get 8-bit (0-248).

function parsePalFile(filePath) {
    const lines  = fs.readFileSync(filePath, 'utf8').split('\n');
    const result = {};  // section → { name → [c0,c1,c2,c3] }  each color = {r,g,b}
    let section  = null;

    for (const raw of lines) {
        const line = raw.trim();
        if (line.startsWith(';')) {
            const m = line.match(/^;\s*(\w+)/);
            if (m) { section = m[1].toLowerCase(); result[section] = {}; }
            continue;
        }
        if (!line.startsWith('RGB')) continue;
        // Strip inline comment, parse 12 numbers
        const noComment = line.replace(/;.*$/, '');
        const nums      = noComment.match(/\d+/g);
        if (!nums || nums.length < 12) continue;
        const n = nums.map(Number);
        // Name comes after the last semicolon on the original line
        const nameMatch = raw.match(/;\s*(\w+)\s*$/);
        const name      = nameMatch ? nameMatch[1].toLowerCase() : `pal${Object.keys(result[section]).length}`;
        result[section][name] = [
            { r: n[0]*8, g: n[1]*8,  b: n[2]*8  },
            { r: n[3]*8, g: n[4]*8,  b: n[5]*8  },
            { r: n[6]*8, g: n[7]*8,  b: n[8]*8  },
            { r: n[9]*8, g: n[10]*8, b: n[11]*8 },
        ];
    }
    return result;
}

// ── Parse {tileset}_palette_map.asm ──────────────────────────────────────────
// Format lines: tilepal <bank>, PAL, PAL, …  (8 palette names per line)
// Returns array of palette names indexed by tile index.

function parsePaletteMapAsm(filePath) {
    const lines  = fs.readFileSync(filePath, 'utf8').split('\n');
    const result = [];  // result[tileIdx] = paletteName (lowercase)

    for (const raw of lines) {
        const line = raw.trim();
        if (!line.startsWith('tilepal')) continue;
        // tilepal <bank>, NAME, NAME, NAME, NAME, NAME, NAME, NAME, NAME
        const parts = line.replace('tilepal', '').split(',').map(s => s.trim());
        // parts[0] = bank number, parts[1..8] = palette names
        for (let i = 1; i < parts.length; i++) {
            result.push(parts[i].toLowerCase());
        }
    }
    return result;  // may have up to 208 entries for johto (96 + 16 rept gap + 96)
}

// ── Main ──────────────────────────────────────────────────────────────────────

const allSections = parsePalFile(GFX_PAL);
const day         = allSections['day'];
if (!day) { console.error('Could not find ; day section in', GFX_PAL); process.exit(1); }

// Write bg_palettes.json — day section, RGBA (alpha always 255)
const palOut = {};
for (const [name, colors] of Object.entries(day)) {
    palOut[name] = colors.map(c => ({ r: c.r, g: c.g, b: c.b, a: 255 }));
}
fs.writeFileSync(path.join(OUT_DIR, 'bg_palettes.json'), JSON.stringify(palOut, null, 2));
console.log('Wrote bg_palettes.json —', Object.keys(palOut).join(', '));

// Process each tileset that has a palette map ASM
const tilesets = fs.readdirSync(PAL_MAP)
    .filter(f => f.endsWith('_palette_map.asm') && !f.startsWith('tileset_'))
    .map(f => f.replace('_palette_map.asm', ''));

for (const tileset of tilesets) {
    const asmPath = path.join(PAL_MAP, `${tileset}_palette_map.asm`);
    const outPath = path.join(OUT_DIR, `${tileset}_tile_palettes.bin`);

    const tileNames = parsePaletteMapAsm(asmPath);

    // Map palette names to indices (order matches bg_tiles.pal section order)
    const nameOrder = ['gray', 'red', 'green', 'water', 'yellow', 'brown', 'roof', 'text'];
    const nameToIdx = Object.fromEntries(nameOrder.map((n, i) => [n, i]));

    // Build 256-byte buffer; default to 0 (gray) for out-of-range tiles
    const buf = Buffer.alloc(256, 0);
    for (let i = 0; i < Math.min(tileNames.length, 256); i++) {
        const name = tileNames[i];
        buf[i] = nameToIdx[name] ?? 0;
    }

    fs.writeFileSync(outPath, buf);
    console.log(`Wrote ${tileset}_tile_palettes.bin (${tileNames.length} entries parsed)`);
}

// ── NPC sprite palettes ───────────────────────────────────────────────────────
// Crystal PAL_OW_* constants order (sprite_data_constants.asm):
//   0=red, 1=blue, 2=green, 3=brown, 4=pink, 5=emote/silver, 6=tree, 7=rock
// npc_sprites.pal lists them as: red, blue, green, brown, pink, silver, tree, rock

const npcSections = parsePalFile(NPC_PAL);
const npcDay = npcSections['day'];
if (npcDay) {
    // Color 0 is always transparent for sprites (GBC sprite hardware).
    // Output with explicit transparent flag on color index 0.
    const npcOut = {};
    for (const [name, colors] of Object.entries(npcDay)) {
        npcOut[name] = colors.map((c, i) => ({
            r: c.r, g: c.g, b: c.b,
            a: i === 0 ? 0 : 255   // color 0 = transparent
        }));
    }
    fs.writeFileSync(path.join(SPRITES_DIR, 'npc_sprite_palettes.json'), JSON.stringify(npcOut, null, 2));
    console.log('Wrote npc_sprite_palettes.json —', Object.keys(npcOut).join(', '));
} else {
    console.warn('WARNING: could not find ; day section in npc_sprites.pal');
}

console.log('Done.');
