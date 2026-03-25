namespace PokeCrystal.Rendering;

using PokeCrystal.Schema;

/// <summary>
/// Default IPaletteManager. Pure logic — no MonoGame dependency.
/// Manages the active palette slots and drives fade/tint state.
/// The renderer queries FadeLevel and WorldTint each frame to apply effects.
/// </summary>
public sealed class PaletteManager : IPaletteManager
{
    private readonly Dictionary<string, Palette> _slots = new(StringComparer.Ordinal);

    // Fade state
    private enum FadeTarget { None, Black, White }
    private FadeTarget _fadeTarget   = FadeTarget.None;
    private bool       _fadeIn       = false;   // true = fading from, false = fading to
    private float      _fadeDuration = 0f;
    private float      _fadeElapsed  = 0f;

    // Tint
    private uint  _tintColor     = 0xFFFFFFFF;
    private float _tintIntensity = 0f;

    // -----------------------------------------------------------------------
    // IPaletteManager — palettes
    // -----------------------------------------------------------------------

    public void LoadPaletteSet(PaletteSet paletteSet)
    {
        _slots.Clear();
        foreach (var palette in paletteSet.Palettes)
            _slots[palette.Id] = palette;
    }

    public Palette? GetPalette(string slotName)
        => _slots.TryGetValue(slotName, out var p) ? p : null;

    public void SetPalette(string slotName, Palette palette)
        => _slots[slotName] = palette;

    // -----------------------------------------------------------------------
    // IPaletteManager — tint
    // -----------------------------------------------------------------------

    public void SetWorldTint(uint color, float intensity)
    {
        _tintColor = color;
        _tintIntensity = Math.Clamp(intensity, 0f, 1f);
    }

    public void ClearWorldTint() => _tintIntensity = 0f;

    public (uint Color, float Intensity) WorldTint => (_tintColor, _tintIntensity);

    // -----------------------------------------------------------------------
    // IPaletteManager — fades
    // -----------------------------------------------------------------------

    public void FadeToBlack(float durationSec)  => StartFade(FadeTarget.Black, fadeIn: false, durationSec);
    public void FadeToWhite(float durationSec)  => StartFade(FadeTarget.White, fadeIn: false, durationSec);
    public void FadeFromBlack(float durationSec) => StartFade(FadeTarget.Black, fadeIn: true,  durationSec);
    public void FadeFromWhite(float durationSec) => StartFade(FadeTarget.White, fadeIn: true,  durationSec);

    private void StartFade(FadeTarget target, bool fadeIn, float durationSec)
    {
        _fadeTarget   = target;
        _fadeIn       = fadeIn;
        _fadeDuration = Math.Max(durationSec, 0.001f);
        _fadeElapsed  = 0f;
    }

    // -----------------------------------------------------------------------
    // IPaletteManager — state
    // -----------------------------------------------------------------------

    public float FadeLevel
    {
        get
        {
            if (_fadeTarget == FadeTarget.None) return 0f;
            float t = _fadeDuration > 0f ? Math.Clamp(_fadeElapsed / _fadeDuration, 0f, 1f) : 1f;
            return _fadeIn ? 1f - t : t;
        }
    }

    public bool IsFadedToBlack => _fadeTarget == FadeTarget.Black && FadeLevel >= 1f;
    public bool IsFadedToWhite => _fadeTarget == FadeTarget.White && FadeLevel >= 1f;

    // -----------------------------------------------------------------------
    // IPaletteManager — update
    // -----------------------------------------------------------------------

    public void Update(float deltaTime)
    {
        if (_fadeTarget == FadeTarget.None) return;

        _fadeElapsed += deltaTime;
        if (_fadeElapsed >= _fadeDuration)
        {
            _fadeElapsed = _fadeDuration;
            if (_fadeIn)
                _fadeTarget = FadeTarget.None; // fade-in complete → no fade active
        }
    }
}
