"""
extract_encounters.py — Extract wild encounter tables to data/base/encounters/{MAP_ID}.json.

Reads:
  data/wild/johto_grass.asm    — Johto grass encounters (7 slots × morn/day/nite)
  data/wild/kanto_grass.asm    — Kanto grass encounters
  data/wild/johto_water.asm    — Johto water encounters (3 slots, single rate)
  data/wild/kanto_water.asm    — Kanto water encounters

Output schema:
  {
    "mapId": "SPROUT_TOWER_2F",
    "grass": {
      "mornRate": 2, "dayRate": 2, "niteRate": 2,
      "morn": [{"level": 3, "speciesId": "RATTATA"}, ...],
      "day":  [...],
      "nite": [...]
    },
    "water": {
      "rate": 2,
      "slots": [{"level": 15, "speciesId": "WOOPER"}, ...]
    }
  }

NOTE: DataLoader does not yet load encounters. This extractor stages the data for
a future L1 extension (EncounterLoader). One JSON per map; maps with both grass
and water get a merged file.
"""

import argparse
import json
import re
from pathlib import Path

_COMMENT_RE = re.compile(r"\s*;.*$")


# -----------------------------------------------------------------------
# Grass parser
# def_grass_wildmons MAP_ID
# db MORN_RATE percent, DAY_RATE percent, NITE_RATE percent
# ; morn     (7 × "db level, SPECIES")
# ; day      (7 × "db level, SPECIES")
# ; nite     (7 × "db level, SPECIES")
# end_grass_wildmons
# -----------------------------------------------------------------------

_GRASS_START_RE = re.compile(r"^\s*def_grass_wildmons\s+(\w+)", re.IGNORECASE)
_GRASS_END_RE   = re.compile(r"^\s*end_grass_wildmons\b", re.IGNORECASE)
_WATER_START_RE = re.compile(r"^\s*def_water_wildmons\s+(\w+)", re.IGNORECASE)
_WATER_END_RE   = re.compile(r"^\s*end_water_wildmons\b", re.IGNORECASE)
_RATES_RE       = re.compile(
    r"^\s*db\s+(\d+)\s*percent\s*,\s*(\d+)\s*percent\s*,\s*(\d+)\s*percent",
    re.IGNORECASE,
)
_RATE_RE        = re.compile(r"^\s*db\s+(\d+)\s*percent", re.IGNORECASE)
_SLOT_RE        = re.compile(r"^\s*db\s+(\d+)\s*,\s*(\w+)", re.IGNORECASE)


def parse_grass_file(path: Path) -> dict[str, dict]:
    """Returns {MAP_ID: grass_table_dict}."""
    result: dict[str, dict] = {}
    current_map: str | None = None
    state = "idle"   # idle | rates | morn | day | nite
    entry: dict = {}

    for raw_line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        raw_lower = raw_line.lower()

        # Section headers are pure comment lines — check BEFORE stripping
        if current_map and state != "rates":
            # Match lines that are purely/mainly a section comment (no data args)
            if re.search(r";\s*morn\b", raw_lower) and not _SLOT_RE.search(raw_lower):
                state = "morn"
                continue
            if re.search(r";\s*day\b", raw_lower) and not _SLOT_RE.search(raw_lower):
                state = "day"
                continue
            if re.search(r";\s*nite\b", raw_lower) and not _SLOT_RE.search(raw_lower):
                state = "nite"
                continue

        line = _COMMENT_RE.sub("", raw_line).strip()
        if not line:
            continue

        if _GRASS_END_RE.match(line):
            if current_map and entry:
                result[current_map] = entry
            current_map = None
            state = "idle"
            entry = {}
            continue

        m = _GRASS_START_RE.match(line)
        if m:
            current_map = m.group(1)
            state = "rates"
            entry = {"mornRate": 0, "dayRate": 0, "niteRate": 0,
                     "morn": [], "day": [], "nite": []}
            continue

        if state == "rates":
            m = _RATES_RE.match(line)
            if m:
                entry["mornRate"] = int(m.group(1))
                entry["dayRate"]  = int(m.group(2))
                entry["niteRate"] = int(m.group(3))
                state = "morn"
            continue

        if state in ("morn", "day", "nite"):
            m = _SLOT_RE.match(line)
            if m:
                slot = {"level": int(m.group(1)), "speciesId": m.group(2)}
                entry[state].append(slot)  # type: ignore[index]

    return result


# -----------------------------------------------------------------------
# Water parser
# def_water_wildmons MAP_ID
# db RATE percent
# db level, SPECIES   (3 entries)
# end_water_wildmons
# -----------------------------------------------------------------------

def parse_water_file(path: Path) -> dict[str, dict]:
    """Returns {MAP_ID: water_table_dict}."""
    result: dict[str, dict] = {}
    current_map: str | None = None
    entry: dict = {}
    in_water = False

    for raw_line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = _COMMENT_RE.sub("", raw_line).strip()
        if not line:
            continue

        if _WATER_END_RE.match(line):
            if current_map and entry:
                result[current_map] = entry
            current_map = None
            entry = {}
            in_water = False
            continue

        m = _WATER_START_RE.match(line)
        if m:
            current_map = m.group(1)
            entry = {"rate": 0, "slots": []}
            in_water = True
            continue

        if not in_water:
            continue

        if entry.get("rate") == 0:
            m = _RATE_RE.match(line)
            if m:
                entry["rate"] = int(m.group(1))
                continue

        m = _SLOT_RE.match(line)
        if m:
            entry["slots"].append({"level": int(m.group(1)), "speciesId": m.group(2)})

    return result


# -----------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------

def extract_encounters(source_root: Path, output_root: Path) -> None:
    wild = source_root / "data" / "wild"

    # Accumulate per-map data
    all_grass: dict[str, dict] = {}
    all_water: dict[str, dict] = {}

    for grass_file in [wild / "johto_grass.asm", wild / "kanto_grass.asm"]:
        if grass_file.exists():
            all_grass.update(parse_grass_file(grass_file))

    for water_file in [wild / "johto_water.asm", wild / "kanto_water.asm"]:
        if water_file.exists():
            all_water.update(parse_water_file(water_file))

    all_maps = set(all_grass) | set(all_water)

    out_dir = output_root / "encounters"
    out_dir.mkdir(parents=True, exist_ok=True)

    for map_id in sorted(all_maps):
        record = {
            "mapId": map_id,
            "grass": all_grass.get(map_id),
            "water": all_water.get(map_id),
        }
        out_path = out_dir / f"{map_id}.json"
        out_path.write_text(json.dumps(record, indent=2), encoding="utf-8")

    print(f"Encounters: {len(all_maps)} maps "
          f"({len(all_grass)} grass, {len(all_water)} water)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract wild encounter data to JSON")
    parser.add_argument("--source", required=True, help="Path to pokecrystal-master/")
    parser.add_argument("--output", required=True, help="Path to data/base/")
    args = parser.parse_args()
    extract_encounters(Path(args.source), Path(args.output))
