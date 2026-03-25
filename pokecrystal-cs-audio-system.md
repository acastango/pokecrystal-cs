# pokecrystal-cs: Audio System Specification

## Layer Placement

Audio spans two layers:
- **Layer 1 (Data):** Audio asset loading, format decoding, soundfont management, music/SFX data definitions
- **Layer 5 (Rendering):** Mixer, playback engine, channel management, spatial audio, MonoGame integration

The split matters for modding — a mod that adds new music or swaps the soundfont only touches Layer 1 data. A mod that changes how audio is mixed or adds real-time effects needs a Layer 5 plugin.

---

## Design Philosophy

The base game ships with a faithful Game Boy APU sound. But the audio engine underneath is a modern multi-channel mixer that happens to *default* to GB-style synthesis. A modder can swap the soundfont from chiptune to orchestral, add reverb, use pre-rendered audio files, or mix all of these — without touching the engine.

The key abstraction: **music is authored as note/event data, not as waveform audio.** The engine interprets note data through whatever instrument set is loaded. The same song definition can sound like a Game Boy, a SNES, a piano, or a full orchestra depending on the soundfont and renderer attached to it.

For mods that don't want to deal with note data at all, the system also supports direct audio file playback (OGG, WAV, MP3) as a first-class alternative. A mod can replace any BGM track with a pre-rendered audio file and the engine treats it identically.

---

## Supported Formats

### Music Authoring Formats

| Format | Use Case | How It Works |
|--------|----------|-------------|
| **MIDI** (.mid) | Primary music authoring format | Standard, universal, every DAW exports it. The engine plays MIDI through the loaded soundfont. Base game ships MIDI files converted from Crystal's music data. |
| **MML** (custom text) | Retro/chiptune authoring | Music Macro Language — the text-based format ROM hackers and chiptune composers already use. Closer to how Crystal's music is actually authored. Compiled to MIDI-equivalent event data at load time. |
| **Direct audio** (.ogg, .wav, .mp3) | Pre-rendered music | For mods shipping studio-produced tracks. Bypasses the sequencer and soundfont entirely — streamed directly to the mixer. OGG is preferred for size; WAV for lossless; MP3 for compatibility. |

MIDI is the backbone. MML is a convenience layer for the retro community. Direct audio is the escape hatch for anything that doesn't fit a sequenced model.

### SFX Formats

| Format | Use Case |
|--------|----------|
| **WAV** (.wav) | Short sound effects — hits, menu sounds, cries. Loaded fully into memory. |
| **OGG** (.ogg) | Longer effects or compressed variants. Decoded on load or streamed. |
| **Synthesized** (engine-generated) | GB-style square/noise synthesis for authentic retro SFX. Defined in JSON as waveform parameters. |

### Soundfont Formats

| Format | Description |
|--------|------------|
| **SF2** (.sf2) | SoundFont 2.0 — the standard for MIDI instrument banks. Widely supported, huge library of free soundfonts available. Base game ships a custom SF2 with GB-accurate square, wave, and noise instruments. |
| **SFZ** (.sfz) | Text-based sampler format. More flexible than SF2 for custom instruments. Each instrument is a folder of WAV samples plus a text definition file. Easier for modders to create and edit. |

The engine supports both. SF2 for drop-in soundfont swaps (thousands available online). SFZ for custom instrument creation. A mod can ship either or both.

---

## Architecture

### Layer 1 Components (Data)

**AudioRegistry** — parallel to DataRegistry for game data. Manages all loaded audio assets: music tracks (MIDI or direct audio), SFX (WAV/OGG/synthesized), soundfonts (SF2/SFZ), and audio config. Supports mod overlay — a mod's audio files override or extend the base set by key.

**MusicData** — a music track definition in JSON that references the actual audio asset:

```json
{
  "id": "new_bark_town",
  "type": "midi",
  "file": "music/new_bark_town.mid",
  "loop_start": 4.2,
  "loop_end": 48.0,
  "volume": 0.85,
  "tags": ["town", "johto", "peaceful"]
}
```

For direct audio:
```json
{
  "id": "custom_battle_theme",
  "type": "stream",
  "file": "music/custom_battle.ogg",
  "loop_start": 0.0,
  "loop_end": 180.0,
  "volume": 1.0,
  "tags": ["battle", "custom"]
}
```

**SfxData** — sound effect definition:

```json
{
  "id": "sfx_damage_normal",
  "type": "sample",
  "file": "sfx/damage_normal.wav",
  "volume": 0.9,
  "pitch_variance": 0.05
}
```

For synthesized retro SFX:
```json
{
  "id": "sfx_damage_normal_retro",
  "type": "synth",
  "channels": [
    {
      "waveform": "noise",
      "envelope": { "attack": 0, "decay": 80, "sustain": 0, "release": 40 },
      "frequency": 440,
      "duration_ms": 120
    }
  ]
}
```

**SoundfontConfig** — which soundfont to use:

```json
{
  "default_soundfont": "soundfonts/gb_apu.sf2",
  "overrides": {
    "battle": "soundfonts/battle_orchestral.sf2"
  }
}
```

The `overrides` field allows context-specific soundfont swaps — battle music through one soundfont, overworld through another. Optional; defaults to the single default soundfont for everything.

**MML Compiler** — converts MML text files to the engine's internal event format at load time. This is a data pipeline step, not a runtime component. MML files are authored as text, compiled to event sequences on load, and played through the same sequencer as MIDI.

### Layer 5 Components (Rendering)

**IAudioRenderer** — the top-level interface. Default implementation uses MonoGame's audio stack, but the interface allows replacement (e.g., FMOD, OpenAL, or a custom DSP pipeline via a mod plugin).

```csharp
public interface IAudioRenderer
{
    void Initialize(AudioRegistry registry);
    void PlayMusic(string trackId, float fadeInSeconds = 0.5f);
    void StopMusic(float fadeOutSeconds = 0.5f);
    void CrossfadeMusic(string trackId, float durationSeconds = 1.0f);
    void PlaySfx(string sfxId);
    void PlaySfx(string sfxId, float pan, float pitch);
    void SetMusicVolume(float volume);
    void SetSfxVolume(float volume);
    void SetMasterVolume(float volume);
    void Update(float deltaTime);
    void Pause();
    void Resume();
}
```

**Sequencer** — reads MIDI (or MML-compiled) event data and dispatches note-on/note-off/control-change events to the synthesizer. Handles tempo, looping (respects `loop_start`/`loop_end` from MusicData), and channel mapping. The sequencer is clock-driven — it ticks forward based on delta time from the game loop, not real-time wall clock.

**Synthesizer** — receives note events from the sequencer and generates audio samples using the loaded soundfont. This is where SF2/SFZ instruments are rendered into waveform data. For the base game's GB mode, the synthesizer uses a specialized GB APU emulation soundfont that produces authentic square/wave/noise tones.

**Mixer** — combines all active audio sources (sequenced music, streamed audio, SFX, synthesized retro sounds) into a single output buffer. Handles:
- Per-channel volume
- Master volume, music volume, SFX volume (separate sliders)
- Fade in/out and crossfade between tracks
- Priority-based voice management (if too many simultaneous sounds, lower-priority ones are culled)
- Panning (for positional SFX if desired)

**StreamPlayer** — handles direct audio file playback (OGG/WAV/MP3). Decodes and streams to the mixer without going through the sequencer or synthesizer. Used for pre-rendered music tracks and long audio files.

**SfxPlayer** — manages short sound effect playback. Supports concurrent SFX with priority and overlap rules. Handles the SFX-suspends-music-channel behavior from the original (optional, configurable — a mod with orchestral music probably doesn't want SFX cutting music channels).

**RetroSynth** — a specialized synthesizer for generating GB-style waveforms from JSON definitions. Produces square waves (variable duty cycle), programmable wave channel output, and noise (LFSR-based). Used only for `"type": "synth"` SFX entries. This is the component that preserves authentic GB sound when desired.

---

## Playback Modes

The engine supports three playback modes that can coexist. A mod chooses per-track which mode to use:

### Sequenced Mode (MIDI + Soundfont)

Music data → Sequencer → Synthesizer (with loaded SF2/SFZ) → Mixer → Output

This is the default for base game music. The same MIDI file sounds different depending on the soundfont. Ship a GB APU soundfont for authenticity, swap to a General MIDI soundfont for "enhanced," swap to a custom orchestral soundfont for a cinematic mod.

### Streamed Mode (Direct Audio)

Audio file → StreamPlayer → Mixer → Output

Bypasses sequencing entirely. The track is a pre-rendered audio file. Used when a modder wants full control over the final sound — studio-produced music, voice acting, ambient soundscapes.

### Hybrid Mode

Some channels sequenced, some streamed. For example: MIDI-driven melody through an orchestral soundfont, plus a streamed ambient layer underneath, plus retro-synth SFX on top. The mixer doesn't care where its input comes from — it combines everything.

---

## The Base Game Soundfont

The base game ships with a custom SF2 soundfont that emulates the Game Boy APU:

| MIDI Program | Instrument | GB Equivalent |
|-------------|-----------|---------------|
| 0 | Square 12.5% duty | Channel 1/2, duty 00 |
| 1 | Square 25% duty | Channel 1/2, duty 01 |
| 2 | Square 50% duty | Channel 1/2, duty 10 |
| 3 | Square 75% duty | Channel 1/2, duty 11 |
| 4 | Programmable wave | Channel 3 (custom waveform) |
| 5–8 | Wave presets | Channel 3 common waveforms from Crystal |
| Percussion bank | Noise channel | Channel 4 (LFSR noise at various rates) |

This soundfont is generated as part of the extraction tooling — the Python scripts that parse Crystal's audio data also produce the SF2 with sampled/synthesized GB waveforms. The MIDI files reference these program numbers, so the base game sounds correct out of the box.

A modder replaces this SF2 with any General MIDI soundfont and the same MIDI files suddenly sound like real instruments. The program number mapping (0 = piano, 40 = violin, etc.) follows the General MIDI spec, so generic soundfonts work without remapping. Modders can also remap individual tracks to specific programs via the MusicData JSON.

---

## Crystal Music Conversion Pipeline

The extraction tooling converts Crystal's music data to MIDI:

1. **Parse** `audio/` ASM files — read note events, tempo, channel assignments, loop points, duty cycle changes, volume envelopes, pitch bends, vibrato commands.

2. **Map channels** — Crystal's 4 channels map to MIDI channels. Channel 1/2 (square) → MIDI channels 0/1 with program number set by duty cycle. Channel 3 (wave) → MIDI channel 2 with program 4. Channel 4 (noise) → MIDI channel 9 (percussion).

3. **Convert events** — note on/off with velocity derived from volume envelope. Tempo changes become MIDI tempo events. Pitch slides become MIDI pitch bend. Vibrato becomes MIDI modulation (CC#1). Duty cycle changes become MIDI program changes.

4. **Embed loop points** — MIDI doesn't natively support loop markers, so loop_start and loop_end are stored in the MusicData JSON alongside the MIDI file. The sequencer reads these to handle looping.

5. **Output** — one `.mid` file per track, one `music_data.json` entry per track, and the GB APU `.sf2` soundfont.

The conversion is lossy in some edge cases — Crystal's audio engine has quirks (channel-specific volume envelopes, noise LFSR modes, pitch sweep on channel 1) that don't map 1:1 to MIDI. The GB APU soundfont compensates for most of this by baking the envelope shapes into the samples. Document edge cases in `layer(5).sub(asm)` nodes.

---

## Mod Surface

### Data Mods (JSON + Audio Files)

The simplest audio modding tier. No code required.

**Replace a track:** Drop an OGG/WAV file and a MusicData JSON entry with the same `id` as the base track. The mod's version overrides the base.

**Add new tracks:** Add new MusicData entries with new IDs. Map them to maps or events via the map data or script data.

**Change the soundfont:** Ship an SF2/SFZ in the mod's `soundfonts/` folder and set it as default in `soundfont_config.json`. All MIDI tracks now play through the new instruments.

**Add sound effects:** Drop WAV/OGG files and SfxData JSON entries. Reference them from scripts or the battle event system.

**Modify existing SFX:** Override an SfxData entry by ID. Change the sample file, volume, pitch variance, or switch from sample to synth (or vice versa).

### Script Mods (Lua)

Play audio from event scripts:

```lua
play_music("my_mod_battle_theme")
play_sfx("my_mod_custom_sound")
crossfade_music("new_bark_town", 2.0)
stop_music(1.0)
set_music_volume(0.5)
```

### Plugin Mods (C# DLL)

Replace the entire audio renderer by implementing `IAudioRenderer`. Use cases: integrating FMOD or Wwise for advanced audio, adding real-time DSP effects (reverb, chorus, EQ), implementing adaptive music that responds to gameplay state (tension-based layering, dynamic instrument addition).

Register custom synthesizers for new instrument types beyond SF2/SFZ.

---

## Pokémon Cries

Cries deserve special treatment because they're per-species and there are 251+ of them.

### Base Game Cries

Crystal's cries are synthesized from two parameters: a base cry sample (shared among evolution lines) with pitch and speed modifiers per species. The extraction tooling pre-renders each species' cry as a short WAV file. These are loaded as SFX entries.

```json
{
  "id": "cry_bulbasaur",
  "type": "sample",
  "file": "cries/bulbasaur.wav",
  "volume": 0.8,
  "species": "bulbasaur"
}
```

### Modded Cries

A mod adding new species includes cry WAV files. A mod replacing existing cries overrides by species key. The cry system is just the SFX system with a species-keyed lookup on top — nothing special architecturally.

For mods that want synthesized cries (matching the GB aesthetic), the RetroSynth can generate cries from parameter definitions:

```json
{
  "id": "cry_fakemon",
  "type": "synth",
  "channels": [
    {
      "waveform": "square_50",
      "pitch_start": 880,
      "pitch_end": 220,
      "pitch_sweep_ms": 400,
      "envelope": { "attack": 0, "decay": 300, "sustain": 0.3, "release": 100 },
      "duration_ms": 500
    },
    {
      "waveform": "noise",
      "envelope": { "attack": 20, "decay": 100, "sustain": 0, "release": 50 },
      "duration_ms": 200,
      "delay_ms": 150
    }
  ]
}
```

---

## Configuration

Global audio settings in the game config:

```json
{
  "audio": {
    "master_volume": 1.0,
    "music_volume": 0.8,
    "sfx_volume": 1.0,
    "sample_rate": 44100,
    "buffer_size": 2048,
    "max_concurrent_sfx": 8,
    "sfx_suspends_music_channel": false,
    "crossfade_default_seconds": 0.5,
    "enable_retro_synth": true
  }
}
```

`sfx_suspends_music_channel` defaults to false — this is the GB behavior where SFX literally takes over a music channel. It's authentic but sounds bad with modern multi-channel audio. Mods targeting retro authenticity can enable it. The mixer handles both modes.

`enable_retro_synth` controls whether the RetroSynth component is available. Mods that don't use any synthesized SFX can disable it to save resources.

---

## MonoGame Integration

MonoGame provides `SoundEffect` (short samples, multiple concurrent instances) and `Song` (streamed, one at a time). The default `IAudioRenderer` implementation maps to these:

- **SFX** → `SoundEffectInstance` pool. Pre-loaded WAV samples, managed by SfxPlayer.
- **Streamed music** → `Song` for OGG/MP3 direct playback.
- **Sequenced music** → The Sequencer + Synthesizer pipeline renders to a `DynamicSoundEffectInstance` buffer that MonoGame streams. This is where MIDI + soundfont audio enters MonoGame's audio graph.

The `DynamicSoundEffectInstance` approach is key — it lets the synthesizer generate audio samples in real-time and feed them to MonoGame's mixer without intermediate files. The synthesizer fills buffers on a background thread; MonoGame consumes them on the audio thread.

### Library Dependencies

| Library | Purpose | License |
|---------|---------|---------|
| **DryWetMIDI** | MIDI file parsing, event handling | MIT |
| **NAudio** or **NFluidsynth** | SF2 soundfont rendering | MIT / LGPL |
| **NVorbis** | OGG decoding (if MonoGame's built-in isn't sufficient) | MIT |
| **MoonSharp** | Already in Layer 3, exposes audio Lua API | MIT-like |

NFluidsynth wraps FluidSynth, the standard open-source SF2 synthesizer. It handles all the complex soundfont rendering (sample interpolation, envelope generators, modulation, filters) so the engine doesn't have to reimplement it. If LGPL is a concern, NAudio's soundfont support is an alternative with a friendlier license.

---

## Adaptable Tile System Parallel

Just as the tile system parameterizes geometry (tile size, block grid, collision resolution) so that mods aren't locked to 8×8 tiles, the audio system parameterizes instrumentation. The base game's "tile size" is 4 GB APU channels with square/wave/noise synthesis. But the system supports arbitrary channel counts, arbitrary instruments, and arbitrary audio formats. A mod can go from chiptune to full orchestra the same way a tileset mod goes from 8×8 to 16×16 — by changing data, not code.

---

## What This Does NOT Cover

- **Music composition.** This spec defines how audio is played, not how it's authored. Composing new MIDI tracks or recording audio files is outside the engine.
- **Voice acting.** Supported implicitly (stream an OGG file), but no dedicated voice system (lip sync, subtitle integration, dialogue audio management).
- **Spatial/3D audio.** The engine is 2D. Panning is supported for positional SFX (left/right based on screen position), but no 3D spatialization.
- **Adaptive music layering.** The interface supports it (a plugin can implement `IAudioRenderer` with adaptive logic), but the base engine doesn't ship with it. The crossfade system handles simple contextual transitions (overworld → battle → victory).
