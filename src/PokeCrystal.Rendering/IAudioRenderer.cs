namespace PokeCrystal.Rendering;

using PokeCrystal.Data;

/// <summary>
/// Top-level audio playback interface. Default implementation is MonoGameAudioRenderer.
/// Mods can replace this entirely (e.g., FMOD, OpenAL) via DI.
/// Music track and SFX IDs are resolved against the AudioRegistry passed at init.
/// </summary>
public interface IAudioRenderer
{
    void Initialize(AudioRegistry registry);

    // --- Music ---

    void PlayMusic(string trackId, float fadeInSeconds = 0.5f);
    void StopMusic(float fadeOutSeconds = 0.5f);
    void CrossfadeMusic(string trackId, float durationSeconds = 1.0f);

    // --- SFX ---

    void PlaySfx(string sfxId);
    void PlaySfx(string sfxId, float pan, float pitch);

    // --- Volume ---

    void SetMusicVolume(float volume);
    void SetSfxVolume(float volume);
    void SetMasterVolume(float volume);

    // --- Lifecycle ---

    void Update(float deltaTime);
    void Pause();
    void Resume();
}
