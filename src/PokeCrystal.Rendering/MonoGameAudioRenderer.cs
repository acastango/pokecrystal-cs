namespace PokeCrystal.Rendering;

using System.IO;
using Microsoft.Xna.Framework.Audio;
using PokeCrystal.Data;

/// <summary>
/// IAudioRenderer backed by MonoGame's SoundEffect / SoundEffectInstance.
/// Music tracks are loaded as SoundEffect and played as looping instances.
/// SFX are pooled SoundEffectInstances (max MaxConcurrentSfx simultaneous).
/// Fades are driven by volume delta applied in Update().
/// </summary>
public sealed class MonoGameAudioRenderer : IAudioRenderer
{
    private const int MaxConcurrentSfx = 8;

    private AudioRegistry? _registry;

    // Loaded assets
    private readonly Dictionary<string, SoundEffect> _musicAssets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SoundEffect> _sfxAssets   = new(StringComparer.Ordinal);

    // Music playback state
    private SoundEffectInstance? _currentMusic;
    private SoundEffectInstance? _nextMusic;        // target during crossfade
    private string? _currentTrackId;

    private float _masterVolume = 1f;
    private float _musicVolume  = 0.8f;
    private float _sfxVolume    = 1f;
    private bool  _paused;

    // Fade state for music
    private float _musicFadeDir      = 0f;   // +1 = fade in, -1 = fade out
    private float _musicFadeDuration = 0f;
    private float _musicFadeElapsed  = 0f;

    // SFX pool
    private readonly List<SoundEffectInstance> _sfxPool = new(MaxConcurrentSfx);

    // -----------------------------------------------------------------------
    // IAudioRenderer — Init
    // -----------------------------------------------------------------------

    public void Initialize(AudioRegistry registry)
    {
        _registry = registry;

        foreach (var music in registry.AllMusic)
            LoadAsset(music.File, _musicAssets, music.Id);

        foreach (var sfx in registry.AllSfx)
            LoadAsset(sfx.File, _sfxAssets, sfx.Id);
    }

    private static void LoadAsset(string file, Dictionary<string, SoundEffect> dict, string id)
    {
        if (!File.Exists(file)) return;
        try
        {
            using var stream = File.OpenRead(file);
            dict[id] = SoundEffect.FromStream(stream);
        }
        catch { /* skip missing/corrupt audio files gracefully */ }
    }

    // -----------------------------------------------------------------------
    // IAudioRenderer — Music
    // -----------------------------------------------------------------------

    public void PlayMusic(string trackId, float fadeInSeconds = 0.5f)
    {
        if (trackId == _currentTrackId) return;
        StopCurrentMusic(immediate: true);

        if (!_musicAssets.TryGetValue(trackId, out var effect)) return;

        _currentTrackId = trackId;
        _currentMusic = effect.CreateInstance();
        _currentMusic.IsLooped = true;
        _currentMusic.Volume = fadeInSeconds > 0f ? 0f : MusicEffectiveVolume;
        _currentMusic.Play();

        if (!_paused && fadeInSeconds > 0f)
            BeginMusicFade(+1f, fadeInSeconds);
    }

    public void StopMusic(float fadeOutSeconds = 0.5f)
    {
        if (_currentMusic is null) return;
        if (fadeOutSeconds <= 0f)
            StopCurrentMusic(immediate: true);
        else
            BeginMusicFade(-1f, fadeOutSeconds);
    }

    public void CrossfadeMusic(string trackId, float durationSeconds = 1.0f)
    {
        if (trackId == _currentTrackId) return;

        // Fade out current
        if (_currentMusic is not null)
            BeginMusicFade(-1f, durationSeconds);

        // Start next at volume 0 and fade in
        if (!_musicAssets.TryGetValue(trackId, out var effect)) return;

        _nextMusic = effect.CreateInstance();
        _nextMusic.IsLooped = true;
        _nextMusic.Volume = 0f;
        _nextMusic.Play();
        _currentTrackId = trackId;
    }

    private void BeginMusicFade(float direction, float duration)
    {
        _musicFadeDir      = direction;
        _musicFadeDuration = Math.Max(duration, 0.001f);
        _musicFadeElapsed  = 0f;
    }

    private void StopCurrentMusic(bool immediate)
    {
        if (_currentMusic is null) return;
        if (immediate) _currentMusic.Stop();
        _currentMusic.Dispose();
        _currentMusic = null;
        _currentTrackId = null;
        _musicFadeDir = 0f;
    }

    // -----------------------------------------------------------------------
    // IAudioRenderer — SFX
    // -----------------------------------------------------------------------

    public void PlaySfx(string sfxId) => PlaySfx(sfxId, 0f, 1f);

    public void PlaySfx(string sfxId, float pan, float pitch)
    {
        if (!_sfxAssets.TryGetValue(sfxId, out var effect)) return;

        // Reuse a stopped instance from the pool
        SoundEffectInstance? inst = null;
        foreach (var pooled in _sfxPool)
        {
            if (pooled.State == SoundState.Stopped) { inst = pooled; break; }
        }

        if (inst is null)
        {
            if (_sfxPool.Count >= MaxConcurrentSfx) return; // pool full
            inst = effect.CreateInstance();
            _sfxPool.Add(inst);
        }

        inst.Volume = _sfxVolume * _masterVolume;
        inst.Pan    = Math.Clamp(pan, -1f, 1f);
        inst.Pitch  = Math.Clamp(pitch - 1f, -1f, 1f); // MonoGame Pitch: -1..1 (0 = normal)
        inst.Play();
    }

    // -----------------------------------------------------------------------
    // IAudioRenderer — Volume
    // -----------------------------------------------------------------------

    public void SetMasterVolume(float volume) { _masterVolume = Clamp01(volume); ApplyMusicVolume(); }
    public void SetMusicVolume(float volume)  { _musicVolume  = Clamp01(volume); ApplyMusicVolume(); }
    public void SetSfxVolume(float volume)    { _sfxVolume    = Clamp01(volume); }

    private float MusicEffectiveVolume => _musicVolume * _masterVolume;

    private void ApplyMusicVolume()
    {
        if (_currentMusic is not null && _musicFadeDir == 0f)
            _currentMusic.Volume = MusicEffectiveVolume;
    }

    // -----------------------------------------------------------------------
    // IAudioRenderer — Lifecycle
    // -----------------------------------------------------------------------

    public void Pause()
    {
        _paused = true;
        _currentMusic?.Pause();
        _nextMusic?.Pause();
    }

    public void Resume()
    {
        _paused = false;
        _currentMusic?.Resume();
        _nextMusic?.Resume();
    }

    public void Update(float deltaTime)
    {
        if (_musicFadeDir == 0f) return;

        _musicFadeElapsed += deltaTime;
        float t = _musicFadeDuration > 0f
            ? Math.Clamp(_musicFadeElapsed / _musicFadeDuration, 0f, 1f)
            : 1f;

        if (_musicFadeDir > 0f)
        {
            // Fade in
            if (_currentMusic is not null)
                _currentMusic.Volume = MusicEffectiveVolume * t;
            // Crossfade: fade out next (which is actually the old track stored in next)
            if (_nextMusic is not null)
                _nextMusic.Volume = MusicEffectiveVolume * (1f - t);
        }
        else
        {
            // Fade out
            if (_currentMusic is not null)
                _currentMusic.Volume = MusicEffectiveVolume * (1f - t);
        }

        if (t >= 1f)
        {
            if (_musicFadeDir < 0f)
                StopCurrentMusic(immediate: true);

            if (_nextMusic is not null)
            {
                _nextMusic.Dispose();
                _nextMusic = null;
            }

            _musicFadeDir = 0f;
        }
    }

    // -----------------------------------------------------------------------

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
