"""
extract_pokemon.py — Extract species data from pokecrystal-master/ to data/base/pokemon/.

Reads:
  data/pokemon/base_stats/*.asm        — stats, types, catch rate, etc.
  data/pokemon/names.asm               — display names
  data/pokemon/evos_attacks.asm        — evolutions + learnset
  data/pokemon/egg_moves.asm           — egg moves
  constants/pokemon_constants.asm      — name→int ID table

Writes:
  data/base/pokemon/{SPECIES_ID}.json  — one file per species
"""

import argparse
import json
import re
import sys
from pathlib import Path

from extract_constants import load_all_constants, invert

_COMMENT_RE = re.compile(r"\s*;.*$")

# -----------------------------------------------------------------------
# names.asm / dname macro
# -----------------------------------------------------------------------

def parse_names(path: Path) -> list[str]:
    """Returns list of display names in order (index 0 = unused, 1 = first species)."""
    names: list[str] = [""]  # index 0 is unused
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        m = re.match(r'^\s*dname\s+"([^"]+)"', line, re.IGNORECASE)
        if not m:
            m = re.match(r'^\s*li\s+"([^"]+)"', line, re.IGNORECASE)
        if m:
            names.append(m.group(1))
    return names

# -----------------------------------------------------------------------
# base_stats/*.asm
# -----------------------------------------------------------------------

_DB_ARGS_RE  = re.compile(r"^\s*db\s+(.+)", re.IGNORECASE)
_DW_ARGS_RE  = re.compile(r"^\s*dw\s+(.+)", re.IGNORECASE)
_DN_ARGS_RE  = re.compile(r"^\s*dn\s+(.+)", re.IGNORECASE)
_TMHM_RE     = re.compile(r"^\s*tmhm\s+(.+)", re.IGNORECASE)
_INCBIN_RE   = re.compile(r"^\s*INCBIN\s+", re.IGNORECASE)

_GENDER_RATIOS: dict[str, float] = {
    "GENDER_F0":    0.0,
    "GENDER_F12_5": 0.125,
    "GENDER_F25":   0.25,
    "GENDER_F50":   0.5,
    "GENDER_F75":   0.75,
    "GENDER_F100":  1.0,
    "GENDER_UNKNOWN": -1.0,  # genderless
}

_GROWTH_RATES: dict[str, str] = {
    "GROWTH_MEDIUM_FAST": "MediumFast",
    "GROWTH_ERRATIC":     "Erratic",
    "GROWTH_FLUCTUATING": "Fluctuating",
    "GROWTH_MEDIUM_SLOW": "MediumSlow",
    "GROWTH_FAST":        "Fast",
    "GROWTH_SLOW":        "Slow",
}


def parse_base_stats(path: Path, type_names: dict[int, str]) -> dict:
    """Parse a single base_stats/*.asm file. Returns partial species dict."""
    lines = [_COMMENT_RE.sub("", l).strip()
             for l in path.read_text(encoding="utf-8", errors="replace").splitlines()]
    lines = [l for l in lines if l]

    data: dict = {}
    line_iter = iter(lines)
    db_rows: list[list[str]] = []
    dn_val: list[str] = []
    tmhm_moves: list[str] = []

    for line in line_iter:
        if _INCBIN_RE.match(line) or line.startswith("dw "):
            continue
        m = _TMHM_RE.match(line)
        if m:
            tmhm_moves = [a.strip() for a in m.group(1).split(",")]
            continue
        m = _DN_ARGS_RE.match(line)
        if m:
            dn_val = [a.strip() for a in m.group(1).split(",")]
            continue
        m = _DB_ARGS_RE.match(line)
        if m:
            db_rows.append([a.strip() for a in m.group(1).split(",")])

    # db row 0: species const (ignored — we key by filename)
    # db row 1: hp atk def spd sat sdf
    if len(db_rows) > 1:
        stats = db_rows[1]
        data["baseHp"]      = int(stats[0])
        data["baseAttack"]  = int(stats[1])
        data["baseDefense"] = int(stats[2])
        data["baseSpeed"]   = int(stats[3])
        data["baseSpAtk"]   = int(stats[4])
        data["baseSpDef"]   = int(stats[5])
    # db row 2: type1, type2
    if len(db_rows) > 2:
        t1, t2 = db_rows[2][0], db_rows[2][1]
        data["type1Id"] = t1
        data["type2Id"] = t2
    # db row 3: catch rate
    if len(db_rows) > 3:
        data["catchRate"] = int(db_rows[3][0])
    # db row 4: base exp
    if len(db_rows) > 4:
        data["baseExp"] = int(db_rows[4][0])
    # db row 5: held items (skip)
    # db row 6: gender ratio
    if len(db_rows) > 6:
        gr = db_rows[6][0]
        data["genderRatio"] = _GENDER_RATIOS.get(gr, 0.5)
    # db row 8: hatch cycles
    if len(db_rows) > 8:
        data["hatchCycles"] = int(db_rows[8][0])
    # db row 9: growth rate
    if len(db_rows) > 10:
        data["growthRate"] = _GROWTH_RATES.get(db_rows[10][0], "MediumFast")

    # dn: egg groups
    if dn_val:
        data["eggGroups"] = dn_val

    data["tmHmMoves"] = tmhm_moves  # list of move const strings; resolved to flags later
    return data

# -----------------------------------------------------------------------
# evos_attacks.asm
# -----------------------------------------------------------------------

_EVOLVE_METHODS = {
    "EVOLVE_LEVEL":     "Level",
    "EVOLVE_ITEM":      "Item",
    "EVOLVE_TRADE":     "Trade",
    "EVOLVE_HAPPINESS": "Happiness",
    "EVOLVE_STAT":      "Stat",
}

# How many extra bytes each evolution method uses after the method byte
_EVOLVE_PARAMS: dict[str, int] = {
    "EVOLVE_LEVEL":     1,   # level
    "EVOLVE_ITEM":      1,   # item const
    "EVOLVE_TRADE":     1,   # held item or -1
    "EVOLVE_HAPPINESS": 1,   # time-of-day const
    "EVOLVE_STAT":      2,   # level, ATK_*/DEF comparison
}


def parse_evos_attacks(path: Path, pokemon_by_int: dict[int, str],
                       pokemon_by_name: dict[str, int],
                       move_by_name: dict[str, int]) -> dict[str, dict]:
    """
    Returns {SPECIES_NAME: {evolutions: [...], learnset: [...]}}.
    """
    text = path.read_text(encoding="utf-8", errors="replace")
    result: dict[str, dict] = {}

    # Split into per-species blocks by finding labels like "BulbasaurEvosAttacks:"
    blocks = re.split(r"^(\w+EvosAttacks):", text, flags=re.MULTILINE)

    # blocks[0] = preamble, then alternating [label, content, label, content, ...]
    i = 1
    while i < len(blocks) - 1:
        label = blocks[i]
        content = blocks[i + 1]
        i += 2

        # Derive species name from label (e.g. BulbasaurEvosAttacks → BULBASAUR)
        species_const = _label_to_const(label, pokemon_by_name)
        if not species_const:
            continue

        args = _extract_db_args(content)
        evolutions = []
        learnset = []

        j = 0
        # Parse evolutions
        while j < len(args) and args[j] != "0":
            method_str = args[j]
            if method_str not in _EVOLVE_METHODS:
                j += 1
                continue
            method = _EVOLVE_METHODS[method_str]
            n_extra = _EVOLVE_PARAMS.get(method_str, 1)
            params = args[j+1:j+1+n_extra]
            target = args[j+1+n_extra] if j+1+n_extra < len(args) else ""
            j += 2 + n_extra

            evo = {"method": method, "targetSpeciesId": target}
            if method == "Level" and params:
                evo["param"] = str(_try_int(params[0], 0))  # always string
            elif method == "Item" and params:
                evo["param"] = params[0]  # item ID string
            elif method == "Trade" and params:
                held = params[0]
                evo["param"] = held if held != "-1" else ""
            elif method == "Happiness" and params:
                evo["param"] = params[0]  # ANYTIME/MORNDAY/NITE
            elif method == "Stat" and len(params) >= 2:
                evo["param"] = str(_try_int(params[0], 0))  # level as string
                evo["statCondition"] = params[1]  # ATK_LT/GT/EQ_DEF
            evolutions.append(evo)
        j += 1  # skip terminating 0

        # Parse learnset
        while j < len(args) - 1 and args[j] != "0":
            level_str = args[j]
            move_str  = args[j+1] if j+1 < len(args) else "NO_MOVE"
            if level_str == "0":
                break
            try:
                level = int(level_str)
            except ValueError:
                j += 1
                continue
            learnset.append({"level": level, "moveId": move_str})
            j += 2

        result[species_const] = {"evolutions": evolutions, "learnset": learnset}

    return result


def _label_to_const(label: str, pokemon_by_name: dict[str, int]) -> str | None:
    """BulbasaurEvosAttacks → BULBASAUR (match against known const names)."""
    # Try direct uppercase match first
    upper = label.replace("EvosAttacks", "").upper()
    if upper in pokemon_by_name:
        return upper
    # Brute-force partial match against known names
    for name in pokemon_by_name:
        if name.replace("_", "").upper() == upper.replace("_", ""):
            return name
    return None


def _extract_db_args(content: str) -> list[str]:
    """Extract all comma-separated arguments from db lines in a block."""
    args: list[str] = []
    for line in content.splitlines():
        clean = _COMMENT_RE.sub("", line).strip()
        m = _DB_ARGS_RE.match(clean)
        if m:
            args.extend(a.strip() for a in m.group(1).split(","))
    return args


def _try_int(s: str, default: int) -> int:
    try:
        return int(s)
    except ValueError:
        return default

# -----------------------------------------------------------------------
# egg_moves.asm
# -----------------------------------------------------------------------

def parse_egg_moves(path: Path, pokemon_by_name: dict[str, int]) -> dict[str, list[str]]:
    """Returns {SPECIES_NAME: [move_id, ...]}."""
    text = path.read_text(encoding="utf-8", errors="replace")
    result: dict[str, list[str]] = {}

    blocks = re.split(r"^(\w+EggMoves):", text, flags=re.MULTILINE)
    i = 1
    while i < len(blocks) - 1:
        label = blocks[i]
        content = blocks[i + 1]
        i += 2

        species = _label_to_const(label.replace("EggMoves", "") + "EvosAttacks", pokemon_by_name)
        if not species:
            # Try without the EvosAttacks mangling
            upper = label.replace("EggMoves", "").upper()
            species = upper if upper in pokemon_by_name else None
        if not species:
            continue

        moves: list[str] = []
        for line in content.splitlines():
            clean = _COMMENT_RE.sub("", line).strip()
            m = _DB_ARGS_RE.match(clean)
            if m:
                val = m.group(1).strip()
                if val != "-1" and val != "0":
                    moves.append(val)
        if moves:
            result[species] = moves

    return result

# -----------------------------------------------------------------------
# TM/HM move list → bool[] indexed by TM/HM number
# -----------------------------------------------------------------------

def build_tmhm_flags(tmhm_moves: list[str], tmhm_order: list[str]) -> list[bool]:
    """Convert a list of known TM/HM move names to a bool array indexed by slot."""
    move_set = set(tmhm_moves)
    return [m in move_set for m in tmhm_order]

# -----------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------

def extract_pokemon(source_root: Path, output_root: Path) -> None:
    consts = load_all_constants(source_root)
    pokemon_by_name = consts["pokemon"]
    pokemon_by_int  = invert(pokemon_by_name)

    display_names = parse_names(source_root / "data/pokemon/names.asm")

    evos_data = parse_evos_attacks(
        source_root / "data/pokemon/evos_attacks.asm",
        pokemon_by_int, pokemon_by_name, consts["moves"],
    )
    egg_data = parse_egg_moves(
        source_root / "data/pokemon/egg_moves.asm",
        pokemon_by_name,
    )

    out_dir = output_root / "pokemon"
    out_dir.mkdir(parents=True, exist_ok=True)

    ok = 0
    errors = 0
    for stats_file in sorted((source_root / "data/pokemon/base_stats").glob("*.asm")):
        # Files are lowercase (bulbasaur.asm); constants are uppercase (BULBASAUR).
        species_id = stats_file.stem.upper()

        try:
            stats = parse_base_stats(stats_file, invert(consts["types"]))
        except Exception as e:
            print(f"ERROR parsing {stats_file.name}: {e}", file=sys.stderr)
            errors += 1
            continue

        int_id = pokemon_by_name.get(species_id)
        display_name = (display_names[int_id]
                        if int_id and int_id < len(display_names)
                        else species_id.capitalize())

        tmhm_moves = stats.pop("tmHmMoves", [])  # keep as list of move IDs

        evo_info = evos_data.get(species_id, {"evolutions": [], "learnset": []})

        record = {
            "id":          species_id,
            "name":        display_name,
            "baseHp":      stats.get("baseHp", 0),
            "baseAttack":  stats.get("baseAttack", 0),
            "baseDefense": stats.get("baseDefense", 0),
            "baseSpeed":   stats.get("baseSpeed", 0),
            "baseSpAtk":   stats.get("baseSpAtk", 0),
            "baseSpDef":   stats.get("baseSpDef", 0),
            "type1Id":     stats.get("type1Id", "NORMAL"),
            "type2Id":     stats.get("type2Id", "NORMAL"),
            "growthRate":  stats.get("growthRate", "MediumFast"),
            "eggGroups":   stats.get("eggGroups", []),
            "genderRatio": stats.get("genderRatio", 0.5),
            "catchRate":   stats.get("catchRate", 45),
            "baseExp":     stats.get("baseExp", 64),
            "hatchCycles": stats.get("hatchCycles", 20),
            "tmHmMoves":   tmhm_moves,
            "learnset":    evo_info["learnset"],
            "evolutions":  evo_info["evolutions"],
            "eggMoves":    egg_data.get(species_id, []),
            "spriteId":    species_id.lower(),
            "cryId":       species_id.lower(),
        }

        out_path = out_dir / f"{species_id}.json"
        out_path.write_text(json.dumps(record, indent=2), encoding="utf-8")
        ok += 1

    print(f"Pokemon: {ok} extracted, {errors} errors")


def _parse_tmhm_order(path: Path) -> list[str]:
    """Parse data/moves/tmhm_moves.asm to get TM/HM move IDs in slot order."""
    order: list[str] = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        clean = _COMMENT_RE.sub("", line).strip()
        m = re.match(r"^\s*db\s+(\w+)", clean, re.IGNORECASE)
        if m:
            order.append(m.group(1))
    return order


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract Pokémon data to JSON")
    parser.add_argument("--source", required=True, help="Path to pokecrystal-master/")
    parser.add_argument("--output", required=True, help="Path to data/base/")
    args = parser.parse_args()
    extract_pokemon(Path(args.source), Path(args.output))
