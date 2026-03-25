namespace PokeCrystal.Rendering;

using PokeCrystal.Schema;

/// <summary>
/// Runtime palette management. Manages the active PaletteSet, drives
/// fade/tint transitions, and exposes per-slot overrides.
/// Implemented by PaletteManager; replaceable by mods for custom effects.
/// </summary>
public interface IPaletteManager
{
    // --- Active palette set ---

    void LoadPaletteSet(PaletteSet paletteSet);
    Palette? GetPalette(string slotName);
    void SetPalette(string slotName, Palette palette);

    // --- Tint (world layer only — does NOT apply to UI) ---

    void SetWorldTint(uint color, float intensity);
    void ClearWorldTint();

    // --- Fade transitions ---

    void FadeToBlack(float durationSec);
    void FadeToWhite(float durationSec);
    void FadeFromBlack(float durationSec);
    void FadeFromWhite(float durationSec);

    // --- Frame update ---

    void Update(float deltaTime);

    // --- Current state ---

    float FadeLevel { get; }       // 0 = none, 1 = fully faded
    bool IsFadedToBlack { get; }
    bool IsFadedToWhite { get; }
    (uint Color, float Intensity) WorldTint { get; }
}
