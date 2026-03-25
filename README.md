# pokecrystal-cs

A work-in-progress reimplementation of Pokémon Crystal in C# and MonoGame.

This is a fan project. It does not include any Nintendo ROM data. Game data is extracted from the [pret/pokecrystal](https://github.com/pret/pokecrystal) decompilation using the Python tools in `tools/` — you need to build the ROM yourself first.

---

## What works

- Overworld started, camera isn't perfect yet.
- Ledge hopping with arc animation and shadow sprite
- Map connections (walking between routes) - though they are static and not scrollable
- NPC map objects and interaction scripting
- Wild encounter system - Battle system, transitions, etc, not yet implemented
- Basic battle engine (damage, type effectiveness, catch, experience) <--- not tested, skeleton built, not fully wired
- Designed for map editor (Avalonia)
- Designed with a mod loader from start

## What doesn't (yet)

Pretty much everything else. Trainer battles, UI, menus, audio, save system — all stubbed or partial.

---

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- MonoGame 3.8 (pulled automatically via NuGet)
- The pret/pokecrystal decompilation built to produce a ROM (see below)

## Getting started

```bash
git clone --recurse-submodules https://github.com/acastango/pokecrystal-cs
cd pokecrystal-cs
```

### 1. Extract game data

You need a built copy of the Crystal ROM. Follow the build instructions in `pokecrystal-master/` (the pret decompilation), then run:

```bash
python tools/extract_all.py
```

This writes JSON data files to `data/base/` and copies sprite/tileset assets to `data/sprites/` and `data/tilesets/`.

### 2. Run the game

```bash
dotnet run --project src/PokeCrystal.Game
```

Default controls:

| Action | Keys |
|--------|------|
| Move | WASD or arrow keys |
| Interact | Z |
| Menu | Enter or Escape |
| Debug console | ` (tilde) |

### 3. Run tests

```bash
dotnet test tests/PokeCrystal.Integration
```

---

## Project layout

```
src/
  PokeCrystal.Schema      — shared types, interfaces, data models
  PokeCrystal.Data        — JSON loaders, registries
  PokeCrystal.World       — overworld engine, map systems, player controller
  PokeCrystal.Engine      — battle engine, damage calc, AI
  PokeCrystal.Scripting   — NPC/event script interpreter
  PokeCrystal.Rendering   — audio/palette abstractions
  PokeCrystal.Mods        — mod loader and hot-reload
  PokeCrystal.Game        — MonoGame entry point, scenes, input
  PokeCrystal.Editor      — Avalonia map editor
data/
  base/                   — extracted game data (JSON)
  sprites/                — player/NPC sprites
  tilesets/               — map tileset graphics
tools/                    — Python extraction scripts
tests/                    — integration tests
pokecrystal-master/       — pret/pokecrystal submodule (reference source)
```

---

## Credits

Game data, logic, and assets are derived from Pokémon Crystal, originally developed by Game Freak and published by Nintendo / The Pokémon Company.

The decompilation this project references is [pret/pokecrystal](https://github.com/pret/pokecrystal), maintained by the pret team and contributors.

This project is non-commercial and intended for educational purposes only.
