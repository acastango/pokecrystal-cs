"""
extract_items.py — Extract item data from pokecrystal-master/ to data/base/items.json.

Reads:
  data/items/attributes.asm     — item_attribute macro table
  data/items/names.asm          — display names (li macro)
  constants/item_constants.asm  — name→int mapping
"""

import argparse
import json
import re
from pathlib import Path

from extract_constants import load_all_constants, invert

_COMMENT_RE = re.compile(r"\s*;.*$")

# -----------------------------------------------------------------------
# Pocket mapping from ASM const → schema enum name
# -----------------------------------------------------------------------

_POCKET_MAP: dict[str, str] = {
    "BALL":     "Balls",
    "ITEM":     "Items",
    "KEY_ITEM": "KeyItems",
    "PC_ITEM":  "PcItems",
}

# Held effect → usability flags heuristic
# (full mapping lives in L2; here we just derive Sellable from price)

# -----------------------------------------------------------------------
# Names (li macro)
# -----------------------------------------------------------------------

def parse_names(path: Path) -> list[str]:
    names: list[str] = [""]  # index 0 = NO_ITEM
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        m = re.match(r'^\s*li\s+"([^"]+)"', line, re.IGNORECASE)
        if m:
            names.append(m.group(1))
    return names

# -----------------------------------------------------------------------
# attributes.asm — item_attribute macro
# item_attribute price, held_effect, param, property, pocket, field_menu, battle_menu
# -----------------------------------------------------------------------

_ATTR_RE = re.compile(
    r"^\s*item_attribute\s+"
    r"(\$?[\da-fA-F]+|\d+),\s*"   # 1: price (may be hex $9999)
    r"(\w+),\s*"                   # 2: held_effect
    r"(\d+),\s*"                   # 3: param
    r"(\w+),\s*"                   # 4: property
    r"(\w+),\s*"                   # 5: pocket
    r"(\w+),\s*"                   # 6: field_menu
    r"(\w+)",                      # 7: battle_menu
    re.IGNORECASE,
)

# Preceding comment line gives us the item const name
_ITEM_COMMENT_RE = re.compile(r"^\s*;\s*(\w+)\s*$")


def parse_attributes(path: Path, item_by_int: dict[int, str]) -> list[dict]:
    text = path.read_text(encoding="utf-8", errors="replace")
    entries: list[dict] = [{"id": "NO_ITEM"}]  # index 0 placeholder

    idx = 1
    last_comment_name: str | None = None

    for line in text.splitlines():
        clean_comment = line.strip()
        cm = _ITEM_COMMENT_RE.match(clean_comment)
        if cm:
            last_comment_name = cm.group(1)
            continue

        clean = _COMMENT_RE.sub("", line)
        m = _ATTR_RE.match(clean)
        if not m:
            continue

        raw_price = m.group(1)
        if raw_price.startswith("$"):
            price = int(raw_price[1:], 16)
        else:
            price = int(raw_price)

        # $9999 sentinel → -1 (unsellable)
        if price == 0x9999:
            price = -1

        pocket_str = _POCKET_MAP.get(m.group(5), "Items")

        # Derive sellable flag: price > 0
        flags = 0
        if price > 0:
            flags |= 16  # Sellable

        # Field/battle menu usability
        if m.group(6) not in ("ITEMMENU_NOUSE",):
            flags |= 1   # Usable
        if m.group(7) not in ("ITEMMENU_NOUSE", "ITEMMENU_CLOSE"):
            flags |= 2   # UsableInBattle
        if m.group(7) == "ITEMMENU_PARTY" or m.group(6) == "ITEMMENU_PARTY":
            flags |= 4   # UsableOnPokemon

        item_id = last_comment_name or item_by_int.get(idx, f"ITEM_{idx}")
        entries.append({
            "id":          item_id,
            "pocket":      pocket_str,
            "price":       price,
            "heldEffect":  m.group(2),
            "heldParam":   int(m.group(3)),
            "flags":       flags,
            "effectKey":   m.group(2),  # L2 maps held effect → effect key
            "flingPower":  0,           # not in ASM — set manually or by L2
        })
        idx += 1
        last_comment_name = None

    return entries


def extract_items(source_root: Path, output_root: Path) -> None:
    consts = load_all_constants(source_root)
    item_by_int = invert(consts["items"])

    names = parse_names(source_root / "data/items/names.asm")
    attrs = parse_attributes(source_root / "data/items/attributes.asm", item_by_int)

    records = []
    for i, entry in enumerate(attrs):
        if entry["id"] == "NO_ITEM":
            continue
        name = names[i] if i < len(names) else entry["id"]
        records.append({
            "id":        entry["id"],
            "name":      name,
            "pocket":    entry["pocket"],
            "price":     entry["price"],
            "flags":     entry["flags"],
            "effectKey": entry["effectKey"],
            "flingPower": entry["flingPower"],
        })

    out_path = output_root / "items.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(records, indent=2), encoding="utf-8")
    print(f"Items: {len(records)} extracted")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract item data to JSON")
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    extract_items(Path(args.source), Path(args.output))
