namespace PokeCrystal.Schema;

/// <summary>
/// Complete save file — mirrors SRAM layout (primary + backup).
/// Integrity is validated by two check values + checksum over game data.
/// On load: validate primary; if corrupt, load backup.
/// PC has 14 boxes × 20 slots each. Active box is kept separately (loaded
/// to WRAM when the player accesses the PC).
/// </summary>
public record SaveFile(
    GameOptions Options,
    PlayerData PlayerData,
    MapPosition MapPosition,
    PokemonData PokemonData,
    List<PcBox> PcBoxes,              // 14 boxes
    PcBox ActiveBox,                  // currently active box (mirrored to WRAM)
    List<HallOfFameEntry> HallOfFame, // 30 entries max
    LinkBattleStats LinkStats,
    MailData Mail,
    CrystalData CrystalData           // GS Ball flag + Crystal-specific extras
);

/// <summary>PC box with a user-assigned name and up to 20 Pokémon slots.</summary>
public record PcBox(string Name, List<StoredPokemon?> Slots);  // nullable = empty slot

/// <summary>Hall of Fame entry — one full-team snapshot.</summary>
public record HallOfFameEntry(List<HofMon> Team);

public record HofMon(string SpeciesId, byte Level, string Nickname, ushort TrainerId);

/// <summary>Link battle win/loss/draw record.</summary>
public record LinkBattleStats(int Wins, int Losses, int Draws, List<LinkBattleRecord> Records);

public record LinkBattleRecord(string OpponentName, bool Won);

/// <summary>Mail storage: 6 party mail slots + 10 mailbox slots.</summary>
public record MailData(List<MailItem?> PartyMail, List<MailItem?> MailboxMail);

public record MailItem(string ItemId, string Message, string AuthorName, ushort AuthorTrainerId);

/// <summary>Crystal-specific extras not present in Gold/Silver.</summary>
public record CrystalData(bool HasGsBall, bool GsBallDelivered);

/// <summary>Persistent game options (text speed, battle animations, etc.).</summary>
public record GameOptions(
    TextSpeed TextSpeed,
    bool BattleAnimations,
    BattleStyleMode BattleStyle,
    SoundMode Sound,
    bool MenuAccount,
    bool FrameStyle       // textbox border style selection
);

public enum TextSpeed { Slow, Mid, Fast, Instant }
public enum BattleStyleMode { Shift, Set }
public enum SoundMode { Mono, Stereo }
