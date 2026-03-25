"""
extract_types.py — Extract type effectiveness data to data/base/type_matchups.json.

Reads:
  data/types/type_matchups.asm   — explicit non-1x entries (NOT_VERY_EFFECTIVE / SUPER_EFFECTIVE)

Adds:
  All immunity (0x) entries not present in the ASM table (they are engine-hardcoded).
"""

import argparse
import json
import re
from pathlib import Path

_COMMENT_RE = re.compile(r"\s*;.*$")

# -----------------------------------------------------------------------
# Multiplier constants in the ASM
# -----------------------------------------------------------------------

_MULTIPLIERS: dict[str, float] = {
    "NOT_VERY_EFFECTIVE": 0.5,
    "SUPER_EFFECTIVE":    2.0,
    "IMMUNE":             0.0,
}

# -----------------------------------------------------------------------
# Immunities hardcoded in the Crystal battle engine (not in the table).
# Sources: engine/battle/core.asm — type immunity checks.
# -----------------------------------------------------------------------

_HARDCODED_IMMUNITIES: list[tuple[str, str]] = [
    ("NORMAL",   "GHOST"),
    ("FIGHTING", "GHOST"),
    ("ELECTRIC", "GROUND"),
    ("POISON",   "STEEL"),
    ("GROUND",   "FLYING"),
    ("PSYCHIC",  "DARK"),
    ("GHOST",    "NORMAL"),
    ("GHOST",    "PSYCHIC"),  # partial in Gen 2 (actually 1x due to bug; model as spec)
]

# -----------------------------------------------------------------------
# Parse type_matchups.asm
# -----------------------------------------------------------------------

_ENTRY_RE = re.compile(
    r"^\s*db\s+(\w+),\s*(\w+),\s*(\w+)",
    re.IGNORECASE,
)


def parse_matchups(path: Path) -> list[dict]:
    entries: list[dict] = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        clean = _COMMENT_RE.sub("", line)
        m = _ENTRY_RE.match(clean)
        if not m:
            continue
        attacker, defender, mult_const = m.group(1), m.group(2), m.group(3)
        if mult_const not in _MULTIPLIERS:
            continue
        entries.append({
            "attackerTypeId": attacker,
            "defenderTypeId": defender,
            "multiplier":     _MULTIPLIERS[mult_const],
        })
    return entries


def extract_types(source_root: Path, output_root: Path) -> None:
    path = source_root / "data/types/type_matchups.asm"
    entries = parse_matchups(path)

    # Add immunity entries (deduplicate against any that might appear in table)
    existing = {(e["attackerTypeId"], e["defenderTypeId"]) for e in entries}
    for attacker, defender in _HARDCODED_IMMUNITIES:
        if (attacker, defender) not in existing:
            entries.append({
                "attackerTypeId": attacker,
                "defenderTypeId": defender,
                "multiplier":     0.0,
            })

    out_path = output_root / "type_matchups.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(entries, indent=2), encoding="utf-8")
    print(f"Type matchups: {len(entries)} entries ({len(_HARDCODED_IMMUNITIES)} immunities added)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract type matchup data to JSON")
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    extract_types(Path(args.source), Path(args.output))
