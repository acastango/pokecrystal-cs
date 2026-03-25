"""
extract_constants.py — Parse RGBDS const_def/const tables into name→int dicts.

Handles:
  const_def [n]       — reset counter to n (default 0)
  const NAME          — assign counter to NAME, then increment
  const_skip [n]      — increment counter by n (default 1) without naming
  DEF NAME EQU const_value  — alias for current counter value (no increment)

Used by all other extractors. Not a standalone script.
"""

import re
from pathlib import Path

_CONST_DEF_RE  = re.compile(r"^\s*const_def\s*(\d+)?", re.IGNORECASE)
_CONST_RE      = re.compile(r"^\s*const\s+(\w+)", re.IGNORECASE)
_CONST_SKIP_RE = re.compile(r"^\s*const_skip\s*(\d+)?", re.IGNORECASE)
# DEF PHYSICAL EQU const_value — aliases current counter without incrementing
_DEF_EQU_RE    = re.compile(r"^\s*DEF\s+(\w+)\s+EQU\s+const_value", re.IGNORECASE)
_COMMENT_RE    = re.compile(r"\s*;.*$")


def parse_constants(path: Path) -> dict[str, int]:
    """
    Parse a RGBDS constants file. Returns {NAME: integer_value}.
    """
    result: dict[str, int] = {}
    counter = 0

    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        clean = _COMMENT_RE.sub("", line)

        m = _CONST_DEF_RE.match(clean)
        if m:
            counter = int(m.group(1)) if m.group(1) else 0
            continue

        m = _CONST_SKIP_RE.match(clean)
        if m:
            counter += int(m.group(1)) if m.group(1) else 1
            continue

        m = _DEF_EQU_RE.match(clean)
        if m:
            result[m.group(1)] = counter  # no increment
            continue

        m = _CONST_RE.match(clean)
        if m:
            result[m.group(1)] = counter
            counter += 1

    return result


def load_all_constants(source_root: Path) -> dict[str, dict[str, int]]:
    """
    Load all relevant constant tables. Returns dict keyed by domain name.
    Inverted tables (int→name) are available via {k: invert(v)}.
    """
    c = source_root / "constants"
    return {
        "pokemon": parse_constants(c / "pokemon_constants.asm"),
        "moves":   parse_constants(c / "move_constants.asm"),
        "items":   parse_constants(c / "item_constants.asm"),
        "types":   parse_constants(c / "type_constants.asm"),
    }


def invert(table: dict[str, int]) -> dict[int, str]:
    """Flip name→int to int→name. Duplicate values keep last assignment."""
    return {v: k for k, v in table.items()}
