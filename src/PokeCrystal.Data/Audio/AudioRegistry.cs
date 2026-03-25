namespace PokeCrystal.Data;

/// <summary>
/// Metadata-only audio asset registry. Holds track and SFX definitions loaded
/// from JSON. Does NOT hold MonoGame objects — those are owned by the renderer.
/// </summary>
public sealed class AudioRegistry
{
    private readonly Dictionary<string, MusicData> _music  = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SfxData>   _sfx    = new(StringComparer.Ordinal);

    public void RegisterMusic(MusicData data) => _music[data.Id]  = data;
    public void RegisterSfx(SfxData data)     => _sfx[data.Id]    = data;

    public MusicData GetMusic(string id)
        => _music.TryGetValue(id, out var m) ? m
           : throw new KeyNotFoundException($"Music '{id}' not registered.");

    public SfxData GetSfx(string id)
        => _sfx.TryGetValue(id, out var s) ? s
           : throw new KeyNotFoundException($"SFX '{id}' not registered.");

    public bool TryGetMusic(string id, out MusicData? data) => _music.TryGetValue(id, out data);
    public bool TryGetSfx(string id, out SfxData? data)     => _sfx.TryGetValue(id, out data);

    public IReadOnlyCollection<MusicData> AllMusic => _music.Values;
    public IReadOnlyCollection<SfxData>   AllSfx   => _sfx.Values;
}

public record MusicData(string Id, string File, float LoopStart, float LoopEnd, float Volume);
public record SfxData(string Id, string File, float Volume);
