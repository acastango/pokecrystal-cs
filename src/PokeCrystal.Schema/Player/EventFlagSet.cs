namespace PokeCrystal.Schema;

/// <summary>
/// Sparse bit array of persistent game-state flags. ~1465 flags in base Crystal.
/// Flags are identified by string keys — not integer indices. Mods register new keys.
/// The first 8 flags are temporary (cleared on map reload).
/// Serialized as {flagKey: true} — only set flags stored.
/// </summary>
public class EventFlagSet
{
    private readonly Dictionary<string, bool> _flags = new();

    public bool Get(string key) => _flags.TryGetValue(key, out var v) && v;

    public void Set(string key, bool value)
    {
        if (value)
            _flags[key] = true;
        else
            _flags.Remove(key);
    }

    public void ClearTemporaryFlags()
    {
        foreach (var key in EventFlags.TemporaryFlags)
            _flags.Remove(key);
    }

    public IReadOnlyDictionary<string, bool> All => _flags;
}

/// <summary>
/// Well-known event flag string constants for base Crystal.
/// Mods register additional keys at load time through the data registry.
/// </summary>
public static class EventFlags
{
    // Temporary flags (cleared on map reload) — indices 0-7
    public const string Temp0 = "EVENT_TEMP_0";
    public const string Temp1 = "EVENT_TEMP_1";
    public const string Temp2 = "EVENT_TEMP_2";
    public const string Temp3 = "EVENT_TEMP_3";
    public const string Temp4 = "EVENT_TEMP_4";
    public const string Temp5 = "EVENT_TEMP_5";
    public const string Temp6 = "EVENT_TEMP_6";
    public const string Temp7 = "EVENT_TEMP_7";

    public static readonly string[] TemporaryFlags =
    [
        Temp0, Temp1, Temp2, Temp3, Temp4, Temp5, Temp6, Temp7,
    ];

    // Badges
    public const string BadgeZephyr    = "EVENT_BADGE_ZEPHYR";
    public const string BadgeHive      = "EVENT_BADGE_HIVE";
    public const string BadgePlain     = "EVENT_BADGE_PLAIN";
    public const string BadgeFog       = "EVENT_BADGE_FOG";
    public const string BadgeStorm     = "EVENT_BADGE_STORM";
    public const string BadgeMineralBadge = "EVENT_BADGE_MINERAL";
    public const string BadgeGlacier   = "EVENT_BADGE_GLACIER";
    public const string BadgeRising    = "EVENT_BADGE_RISING";
    public const string BadgeBoulder   = "EVENT_BADGE_BOULDER";
    public const string BadgeCascade   = "EVENT_BADGE_CASCADE";
    public const string BadgeThunder   = "EVENT_BADGE_THUNDER";
    public const string BadgeRainbow   = "EVENT_BADGE_RAINBOW";
    public const string BadgeSoul      = "EVENT_BADGE_SOUL";
    public const string BadgeMarsh     = "EVENT_BADGE_MARSH";
    public const string BadgeVolcano   = "EVENT_BADGE_VOLCANO";
    public const string BadgeEarth     = "EVENT_BADGE_EARTH";

    // Key items / HMs obtained
    public const string GotHm01Cut     = "EVENT_GOT_HM01_CUT";
    public const string GotHm02Fly     = "EVENT_GOT_HM02_FLY";
    public const string GotHm03Surf    = "EVENT_GOT_HM03_SURF";
    public const string GotHm04Strength = "EVENT_GOT_HM04_STRENGTH";
    public const string GotHm05Flash   = "EVENT_GOT_HM05_FLASH";
    public const string GotHm06Whirlpool = "EVENT_GOT_HM06_WHIRLPOOL";
    public const string GotHm07Waterfall = "EVENT_GOT_HM07_WATERFALL";

    // Story milestones (representative subset — full list populated from constants/)
    public const string BeatFalkner    = "EVENT_BEAT_FALKNER";
    public const string BeatBugsy      = "EVENT_BEAT_BUGSY";
    public const string BeatWhitney    = "EVENT_BEAT_WHITNEY";
    public const string BeatMorty      = "EVENT_BEAT_MORTY";
    public const string BeatChuck      = "EVENT_BEAT_CHUCK";
    public const string BeatJasmine    = "EVENT_BEAT_JASMINE";
    public const string BeatPryce      = "EVENT_BEAT_PRYCE";
    public const string BeatClair      = "EVENT_BEAT_CLAIR";
    public const string BeatElm        = "EVENT_BEAT_ELM";
    public const string BeatLance      = "EVENT_BEAT_LANCE";

    // Pokégear
    public const string GotPokeGear    = "EVENT_GOT_POKEGEAR";
    public const string GotPhoneCard   = "EVENT_GOT_PHONE_CARD";
    public const string GotMapCard     = "EVENT_GOT_MAP_CARD";
    public const string GotRadioCard   = "EVENT_GOT_RADIO_CARD";

    // GS Ball / Crystal-specific
    public const string GotGsBall      = "EVENT_GOT_GS_BALL";
    public const string DeliveredGsBall = "EVENT_DELIVERED_GS_BALL";
    public const string BeatCelebi     = "EVENT_BEAT_CELEBI";
}
