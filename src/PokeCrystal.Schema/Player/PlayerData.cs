namespace PokeCrystal.Schema;

/// <summary>
/// Player identity, inventory, and game progress — mirrors wPlayerData WRAM block.
/// Money is stored as 3-byte BCD in ASM; converted to int on load/save.
/// GameTime caps at 999:59:59 (ASM uses 3 bytes for hours).
/// </summary>
public record PlayerData(
    ushort TrainerId,
    ushort SecretId,          // affects shiny calculation; never shown in-game
    string PlayerName,
    string RivalName,
    Gender PlayerGender,
    int Money,
    int Coins,
    BadgeSet JohtoBadges,
    BadgeSet KantoBadges,
    TmHmSet TmsHMs,
    BagPocket Items,
    BagPocket KeyItems,
    BagPocket Balls,
    BagPocket PcItems,
    PokegearFlags PokegearFlags,
    List<PhoneContact> PhoneContacts,
    PlayerStatusFlags StatusFlags,
    MomData Mom,
    GameTime GameTime,
    TimeOfDay CurrentTimeOfDay
);

/// <summary>8-badge flag set for one region (Johto or Kanto).</summary>
public record BadgeSet(bool[] Badges)  // length 8
{
    public bool Has(int index) => index < Badges.Length && Badges[index];
    public int Count => Badges.Count(b => b);
    public bool AllEight => Badges.All(b => b);
}

/// <summary>TM/HM ownership flags. Indexed by TM/HM number (1-based → index 0-based).</summary>
public record TmHmSet(bool[] Flags);

/// <summary>One slot in a bag pocket: item ID + quantity.</summary>
public record BagSlot(string ItemId, int Quantity);

/// <summary>A bag pocket with a capacity cap (Items=20, KeyItems=26, Balls=12, PcItems=50).</summary>
public record BagPocket(List<BagSlot> Slots, int Capacity);

[Flags]
public enum PokegearFlags
{
    None      = 0,
    Pokegear  = 1 << 0,
    PhoneCard = 1 << 1,
    MapCard   = 1 << 2,
    RadioCard = 1 << 3,
}

/// <summary>Phone contact registered in the Pokégear.</summary>
public record PhoneContact(string TrainerId, string Name, string MapId);

[Flags]
public enum PlayerStatusFlags
{
    None           = 0,
    HasBike        = 1 << 0,
    HasSurfboard   = 1 << 1,
    CanUseFly      = 1 << 2,
}

/// <summary>Mom's savings behavior and accumulated amount.</summary>
public record MomData(bool IsSaving, int Savings);

/// <summary>In-game clock. Hours cap at 999 (ASM: 3 bytes).</summary>
public record GameTime(int Hours, int Minutes, int Seconds)
{
    public const int MaxHours = 999;
}
