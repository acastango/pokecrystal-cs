"""
extract_moves.py — Extract move data from pokecrystal-master/ to data/base/moves.json.

Reads:
  data/moves/moves.asm          — move macro table (animation, effect, power, type, acc, pp, chance)
  data/moves/names.asm          — display names (li macro)
  constants/move_constants.asm  — name→int mapping
  constants/type_constants.asm  — type name→int mapping

Priority and MoveFlags are derived from effect constant via lookup tables (not in ASM data).
"""

import argparse
import json
import re
import sys
from pathlib import Path

from extract_constants import load_all_constants, invert

_COMMENT_RE = re.compile(r"\s*;.*$")

# -----------------------------------------------------------------------
# Priority by effect constant (incomplete — only non-zero entries needed)
# -----------------------------------------------------------------------

_EFFECT_PRIORITY: dict[str, int] = {
    "EFFECT_PRIORITY_HIT":  1,   # Quick Attack, Mach Punch, etc.
    "EFFECT_EXTREMESPEED":  2,
    "EFFECT_COUNTER":      -5,
    "EFFECT_MIRROR_COAT":  -5,
}

# Moves where priority isn't inferrable from effect alone
_MOVE_PRIORITY: dict[str, int] = {
    "VITAL_THROW": -1,   # uses EFFECT_ALWAYS_HIT but has -1 priority
}

# -----------------------------------------------------------------------
# MoveFlags by move name (contact, sound, punch)
# These are not in the ASM data table — derived from effect and move name.
# -----------------------------------------------------------------------

_CONTACT_EFFECTS: set[str] = {
    "EFFECT_NORMAL_HIT", "EFFECT_MULTI_HIT", "EFFECT_SWIFT",
    "EFFECT_RAGE", "EFFECT_SLASH", "EFFECT_CRITICAL_HIT",
    "EFFECT_HIGH_CRITICAL", "EFFECT_DOUBLE_HIT", "EFFECT_JUMP_KICK",
    "EFFECT_STOMP", "EFFECT_THRASH", "EFFECT_PETAL_DANCE",
    "EFFECT_VITAL_THROW", "EFFECT_REVERSAL", "EFFECT_FLAIL",
    "EFFECT_RETURN", "EFFECT_FRUSTRATION", "EFFECT_HIDDEN_POWER",
    "EFFECT_STRENGTH", "EFFECT_WATERFALL",
}

_PUNCH_MOVES: set[str] = {
    "FIRE_PUNCH", "ICE_PUNCH", "THUNDERPUNCH", "COMET_PUNCH",
    "MEGA_PUNCH", "KARATE_CHOP", "DIZZY_PUNCH", "DYNAMIC_PUNCH",
    "SHADOW_PUNCH", "MACH_PUNCH",
}

_SOUND_EFFECTS: set[str] = {
    "EFFECT_GROWL", "EFFECT_ROAR", "EFFECT_SING", "EFFECT_SUPERSONIC",
    "EFFECT_SCREECH", "EFFECT_SNORE", "EFFECT_UPROAR", "EFFECT_PERISH_SONG",
    "EFFECT_HYPER_VOICE", "EFFECT_GRASSWHISTLE",
}

# -----------------------------------------------------------------------
# Names (li macro)
# -----------------------------------------------------------------------

def parse_names(path: Path) -> list[str]:
    names: list[str] = [""]  # index 0 = NO_MOVE placeholder
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        m = re.match(r'^\s*li\s+"([^"]+)"', line, re.IGNORECASE)
        if m:
            names.append(m.group(1))
    return names

# -----------------------------------------------------------------------
# moves.asm — move macro table
# -----------------------------------------------------------------------

_MOVE_RE = re.compile(
    r"^\s*move\s+"
    r"(\w+),\s*"    # 1: move const
    r"(\w+),\s*"    # 2: effect
    r"(\d+),\s*"    # 3: power
    r"(\w+),\s*"    # 4: type
    r"(\d+),\s*"    # 5: accuracy (percent is inside the macro body, not here)
    r"(\d+),\s*"    # 6: pp
    r"(\d+)",       # 7: effect chance
    re.IGNORECASE,
)


def parse_moves(path: Path) -> list[dict]:
    """Returns list of raw move dicts in index order (index 0 = NO_MOVE sentinel)."""
    entries = [{"id": "NO_MOVE", "name": ""}]  # placeholder
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        clean = _COMMENT_RE.sub("", line)
        m = _MOVE_RE.match(clean)
        if m:
            move_const = m.group(1)
            effect     = m.group(2)
            power      = int(m.group(3))
            type_id    = m.group(4)
            acc        = int(m.group(5))
            pp         = int(m.group(6))
            chance     = int(m.group(7))
            entries.append({
                "id":           move_const,
                "effect":       effect,
                "power":        power,
                "typeId":       type_id,
                "accuracy":     acc,
                "pp":           pp,
                "effectChance": chance,
            })
    return entries

# -----------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------

def extract_moves(source_root: Path, output_root: Path) -> None:
    names = parse_names(source_root / "data/moves/names.asm")
    raw   = parse_moves(source_root / "data/moves/moves.asm")

    records = []
    for i, entry in enumerate(raw):
        if entry["id"] == "NO_MOVE":
            continue
        move_id = entry["id"]
        effect  = entry["effect"]

        flags = 0
        if effect in _CONTACT_EFFECTS:
            flags |= 1  # Contact
        if effect in _SOUND_EFFECTS:
            flags |= 2  # Sound
        if move_id in _PUNCH_MOVES:
            flags |= 4  # Punch

        records.append({
            "id":           move_id,
            "name":         names[i] if i < len(names) else move_id,
            "power":        entry["power"],
            "typeId":       entry["typeId"],
            "accuracy":     entry["accuracy"],
            "pp":           entry["pp"],
            "effectChance": entry["effectChance"],
            "effectKey":    effect,
            "priority":     _MOVE_PRIORITY.get(move_id, _EFFECT_PRIORITY.get(effect, 0)),
            "flags":        flags,
            "target":       0,  # SelectedOpponent default; refined in L2
        })

    out_path = output_root / "moves.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(records, indent=2), encoding="utf-8")
    print(f"Moves: {len(records)} extracted")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract move data to JSON")
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    extract_moves(Path(args.source), Path(args.output))
