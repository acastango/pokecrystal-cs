"""
extract_all.py — Run all extractors in dependency order.

Usage:
  python tools/extract_all.py --source pokecrystal-master/ --output data/base/
"""

import argparse
import sys
from pathlib import Path

from extract_pokemon     import extract_pokemon
from extract_moves       import extract_moves
from extract_items       import extract_items
from extract_types       import extract_types
from extract_trainers    import extract_trainers
from extract_encounters  import extract_encounters


def main() -> int:
    parser = argparse.ArgumentParser(description="Extract all Crystal data to JSON")
    parser.add_argument("--source", required=True, help="Path to pokecrystal-master/")
    parser.add_argument("--output", required=True, help="Path to data/base/ output directory")
    args = parser.parse_args()

    source = Path(args.source)
    output = Path(args.output)

    if not source.is_dir():
        print(f"ERROR: source not found: {source}", file=sys.stderr)
        return 1

    output.mkdir(parents=True, exist_ok=True)

    steps = [
        ("Moves",      extract_moves),
        ("Items",      extract_items),
        ("Types",      extract_types),
        ("Pokémon",    extract_pokemon),    # depends on moves (TM/HM flags)
        ("Trainers",   extract_trainers),
        ("Encounters", extract_encounters), # staged — DataLoader extension in L1 needed
    ]

    errors = 0
    for name, fn in steps:
        print(f"\n--- {name} ---")
        try:
            fn(source, output)
        except Exception as e:
            print(f"ERROR in {name}: {e}", file=sys.stderr)
            errors += 1

    print(f"\nDone. {errors} step(s) failed." if errors else "\nAll steps complete.")
    return errors


if __name__ == "__main__":
    sys.exit(main())
