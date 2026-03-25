"""
extract_trainers.py — Extract trainer data to data/base/trainers/{ID}.json.

Reads:
  data/trainers/parties.asm      — trainer name, type, party
  data/trainers/attributes.asm   — per-class AI flags and items
  data/trainers/class_names.asm  — class display names

Trainer ID is built as "{CLASS}_{INDEX}" (e.g. "FALKNER_1").
"""

import argparse
import json
import re
import sys
from pathlib import Path

from extract_constants import _COMMENT_RE

# -----------------------------------------------------------------------
# AI flag → string key mapping
# -----------------------------------------------------------------------

_AI_FLAGS: dict[str, str] = {
    "AI_BASIC":      "basic",
    "AI_SETUP":      "setup",
    "AI_SMART":      "smart",
    "AI_AGGRESSIVE": "aggressive",
    "AI_CAUTIOUS":   "cautious",
    "AI_STATUS":     "status",
    "AI_RISKY":      "risky",
}

# -----------------------------------------------------------------------
# Parse parties.asm
# -----------------------------------------------------------------------

_TRAINER_TYPES = {
    "TRAINERTYPE_NORMAL":     "normal",
    "TRAINERTYPE_MOVES":      "moves",
    "TRAINERTYPE_ITEM":       "item",
    "TRAINERTYPE_ITEM_MOVES": "item_moves",
}


def parse_parties(path: Path) -> list[dict]:
    """
    Returns list of trainer dicts in file order:
    {"classLabel": str, "name": str, "type": str, "party": [...]}
    """
    text = path.read_text(encoding="utf-8", errors="replace")
    trainers: list[dict] = []

    # Split by group labels (e.g. "FalknerGroup:")
    # Each group contains one or more trainers.
    # We scan line by line since the structure is irregular.

    current_class: str | None = None
    current_trainer: dict | None = None
    in_trainer = False

    for raw_line in text.splitlines():
        line = _COMMENT_RE.sub("", raw_line).strip()
        if not line:
            continue

        # Group label
        m = re.match(r"^(\w+Group):\s*$", line)
        if m:
            current_class = m.group(1).replace("Group", "").upper()
            continue

        # Trainer header: db "NAME@", TRAINERTYPE_*
        m = re.match(r'^db\s+"([^"]+)@"\s*,\s*(\w+)', line)
        if m:
            if current_trainer:
                trainers.append(current_trainer)
            current_trainer = {
                "classLabel": current_class or "UNKNOWN",
                "name":       m.group(1),
                "trainerType": _TRAINER_TYPES.get(m.group(2), "normal"),
                "party":      [],
            }
            in_trainer = True
            continue

        # End sentinel
        if line == "db -1":
            if current_trainer:
                trainers.append(current_trainer)
                current_trainer = None
            in_trainer = False
            continue

        # Party entry
        if in_trainer and current_trainer and line.startswith("db "):
            args_str = line[3:].strip()
            args = [a.strip() for a in args_str.split(",")]
            ttype = current_trainer["trainerType"]

            entry: dict = {}
            if ttype == "normal" and len(args) >= 2:
                entry = {"level": _try_int(args[0]), "speciesId": args[1]}
            elif ttype == "moves" and len(args) >= 6:
                entry = {
                    "level": _try_int(args[0]),
                    "speciesId": args[1],
                    "moves": [m for m in args[2:6] if m != "NO_MOVE"],
                }
            elif ttype == "item" and len(args) >= 3:
                entry = {
                    "level": _try_int(args[0]),
                    "speciesId": args[1],
                    "heldItemId": args[2] if args[2] != "NO_ITEM" else None,
                }
            elif ttype == "item_moves" and len(args) >= 7:
                entry = {
                    "level": _try_int(args[0]),
                    "speciesId": args[1],
                    "heldItemId": args[2] if args[2] != "NO_ITEM" else None,
                    "moves": [m for m in args[3:7] if m != "NO_MOVE"],
                }
            if entry:
                current_trainer["party"].append(entry)

    return trainers


def _try_int(s: str) -> int:
    try:
        return int(s)
    except ValueError:
        return 0

# -----------------------------------------------------------------------
# Parse class_names.asm for display names
# -----------------------------------------------------------------------

def parse_class_names(path: Path) -> list[str]:
    names: list[str] = [""]
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        m = re.match(r'^\s*li\s+"([^"]+)"', line, re.IGNORECASE)
        if m:
            names.append(m.group(1))
    return names

# -----------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------

def extract_trainers(source_root: Path, output_root: Path) -> None:
    parties = parse_parties(source_root / "data/trainers/parties.asm")

    out_dir = output_root / "trainers"
    out_dir.mkdir(parents=True, exist_ok=True)

    # Track index per class label for unique IDs
    class_counts: dict[str, int] = {}
    ok = 0

    for t in parties:
        label = t["classLabel"] or "UNKNOWN"
        class_counts[label] = class_counts.get(label, 0) + 1
        trainer_id = f"{label}_{class_counts[label]}"

        record = {
            "id":            trainer_id,
            "classId":       label,
            "name":          t["name"],
            "party":         t["party"],
            "aiStrategyKey": "basic",   # refined per-class from attributes.asm in L2
            "introTextRef":  "",
            "winTextRef":    "",
            "loseTextRef":   "",
        }

        out_path = out_dir / f"{trainer_id}.json"
        out_path.write_text(json.dumps(record, indent=2), encoding="utf-8")
        ok += 1

    print(f"Trainers: {ok} extracted")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract trainer data to JSON")
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    extract_trainers(Path(args.source), Path(args.output))
