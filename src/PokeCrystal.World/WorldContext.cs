namespace PokeCrystal.World;

using PokeCrystal.Schema;
using PokeCrystal.Scripting;

/// <summary>
/// Concrete IScriptContext. Holds the full live world state and implements
/// all world-mutation methods that script commands call.
/// Owned and updated by OverworldEngine each frame.
/// </summary>
public sealed class WorldContext : IScriptContext
{
    // --- IScriptContext VM state ---
    public byte ScriptVar { get; set; }
    public ScriptMode Mode { get; set; }
    public int WaitDelay { get; set; }
    public bool IsMovementComplete { get; set; } = true;

    // --- Map / Player position ---
    public string CurrentMapId { get; set; } = string.Empty;
    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public FacingDirection Facing { get; set; }
    public MapStatus MapStatus { get; set; }
    public bool EventsEnabled { get; set; }
    public int WildEncounterCooldown { get; set; }
    public bool WildEncountersDisabled { get; set; }

    // Pending transitions consumed by OverworldEngine / game layer
    public string? PendingWarpMapId { get; set; }
    public int PendingWarpId { get; set; }
    public BattleSetup? PendingBattle { get; set; }
    public GivePokemonRequest? PendingGivePokemon { get; set; }
    public MovementRequest? PendingMovement { get; set; }
    public string? PendingMusic { get; set; }
    public string? PendingSound { get; set; }
    public string? PendingText { get; set; }
    public string? PendingMenu { get; set; }
    public bool PendingYesNoResult { get; set; }
    public Dictionary<int, bool> ObjectVisibility { get; } = new();

    // --- Core game state ---
    public PlayerData Player { get; set; } = null!;
    public PokemonData Pokemon { get; set; } = null!;
    public EventFlagSet Events { get; } = new();
    public Dictionary<string, int> SceneIds { get; } = new();
    public MapRegistry Maps { get; set; } = null!;

    public TimeOfDay CurrentTimeOfDay { get; set; }

    // Pending wild mon from loadwildmon command
    private string? _pendingWildSpeciesId;
    private int _pendingWildLevel;
    private string? _pendingTrainerId;

    // -----------------------------------------------------------------------
    // IScriptContext — Inventory
    // -----------------------------------------------------------------------

    public bool HasItem(string itemId, int quantity = 1)
    {
        int count = GetAllPockets()
            .SelectMany(p => p.Slots)
            .Where(s => s.ItemId == itemId)
            .Sum(s => s.Quantity);
        return count >= quantity;
    }

    public void GiveItem(string itemId, int quantity)
    {
        var pocket = Player.Items;
        var idx = pocket.Slots.FindIndex(s => s.ItemId == itemId);
        if (idx >= 0)
            pocket.Slots[idx] = pocket.Slots[idx] with { Quantity = pocket.Slots[idx].Quantity + quantity };
        else if (pocket.Slots.Count < pocket.Capacity)
            pocket.Slots.Add(new BagSlot(itemId, quantity));
    }

    public void TakeItem(string itemId, int quantity)
    {
        foreach (var pocket in GetAllPockets())
        {
            var idx = pocket.Slots.FindIndex(s => s.ItemId == itemId);
            if (idx < 0) continue;
            int newQty = pocket.Slots[idx].Quantity - quantity;
            if (newQty <= 0) pocket.Slots.RemoveAt(idx);
            else pocket.Slots[idx] = pocket.Slots[idx] with { Quantity = newQty };
            return;
        }
    }

    public bool BagIsFull(string itemId)
    {
        var pocket = Player.Items;
        bool hasSlot = pocket.Slots.Any(s => s.ItemId == itemId);
        return !hasSlot && pocket.Slots.Count >= pocket.Capacity;
    }

    private IEnumerable<BagPocket> GetAllPockets()
        => [Player.Items, Player.KeyItems, Player.Balls, Player.PcItems];

    // -----------------------------------------------------------------------
    // IScriptContext — Money / Coins
    // -----------------------------------------------------------------------

    public int GetMoney(int account) => account == 0 ? Player.Money : Player.Mom.Savings;

    public void GiveMoney(int account, int amount)
    {
        if (account == 0)
            Player = Player with { Money = Math.Min(Player.Money + amount, 999999) };
        else
            Player = Player with { Mom = Player.Mom with { Savings = Math.Min(Player.Mom.Savings + amount, 999999) } };
    }

    public void TakeMoney(int account, int amount)
    {
        if (account == 0)
            Player = Player with { Money = Math.Max(Player.Money - amount, 0) };
        else
            Player = Player with { Mom = Player.Mom with { Savings = Math.Max(Player.Mom.Savings - amount, 0) } };
    }

    public bool HasMoney(int account, int amount) => GetMoney(account) >= amount;

    public int GetCoins() => Player.Coins;
    public void GiveCoins(int amount) => Player = Player with { Coins = Math.Min(Player.Coins + amount, 9999) };
    public void TakeCoins(int amount) => Player = Player with { Coins = Math.Max(Player.Coins - amount, 0) };
    public bool HasCoins(int amount) => Player.Coins >= amount;

    // -----------------------------------------------------------------------
    // IScriptContext — Party
    // -----------------------------------------------------------------------

    public bool HasPokemon(string speciesId)
        => Pokemon.Party.Any(p => p.Base.SpeciesId == speciesId);

    public void GivePokemon(string speciesId, int level, string heldItemId,
        bool fromTrainer, string? nickname, string? otName)
        => PendingGivePokemon = new(speciesId, level, heldItemId, fromTrainer, nickname, otName);

    public void GiveEgg(string speciesId)
        => PendingGivePokemon = new(speciesId, 5, "NO_ITEM", false, null, null);

    // -----------------------------------------------------------------------
    // IScriptContext — Events / Flags / Scenes
    // -----------------------------------------------------------------------

    public bool CheckEvent(string eventId) => Events.Get(eventId);
    public void SetEvent(string eventId) => Events.Set(eventId, true);
    public void ClearEvent(string eventId) => Events.Set(eventId, false);
    public bool CheckFlag(string flagId) => Events.Get(flagId);
    public void SetFlag(string flagId) => Events.Set(flagId, true);
    public void ClearFlag(string flagId) => Events.Set(flagId, false);

    public int GetScene(string mapId)
        => SceneIds.TryGetValue(mapId, out var s) ? s : 0;
    public void SetScene(string mapId, int sceneId)
        => SceneIds[string.IsNullOrEmpty(mapId) ? CurrentMapId : mapId] = sceneId;

    // -----------------------------------------------------------------------
    // IScriptContext — Phone
    // -----------------------------------------------------------------------

    public bool HasPhoneNumber(string contactId)
        => Player.PhoneContacts.Any(c => c.TrainerId == contactId);

    public void AddPhoneNumber(string contactId)
    {
        if (!HasPhoneNumber(contactId))
            Player.PhoneContacts.Add(new PhoneContact(contactId, string.Empty, string.Empty));
    }

    public void DeletePhoneNumber(string contactId)
        => Player.PhoneContacts.RemoveAll(c => c.TrainerId == contactId);

    // -----------------------------------------------------------------------
    // IScriptContext — World
    // -----------------------------------------------------------------------

    public void Warp(string mapId, int warpId)
    {
        PendingWarpMapId = mapId;
        PendingWarpId = warpId;
        MapStatus = MapStatus.Done;
    }

    public void ApplyMovement(int objectId, string movementScriptId)
    {
        IsMovementComplete = false;
        PendingMovement = new(objectId, movementScriptId);
    }

    public void FacePlayer(int objectId) { /* resolved by NPC system */ }

    public void SetObjectVisible(int objectId, bool visible)
        => ObjectVisibility[objectId] = visible;

    public void PlayMusic(string musicId) => PendingMusic = musicId;
    public void PlaySound(string soundId) => PendingSound = soundId;
    public void WaitSfx() { /* L6 audio system handles */ }

    // -----------------------------------------------------------------------
    // IScriptContext — Battle
    // -----------------------------------------------------------------------

    public void LoadWildMon(string speciesId, int level)
    {
        _pendingWildSpeciesId = speciesId;
        _pendingWildLevel = level;
    }

    public void LoadTrainer(string trainerId) => _pendingTrainerId = trainerId;

    public void StartBattle()
    {
        PendingBattle = _pendingTrainerId is not null
            ? new BattleSetup(null, _pendingTrainerId, 0, false)
            : new BattleSetup(_pendingWildSpeciesId, null, _pendingWildLevel, true);
        _pendingWildSpeciesId = null;
        _pendingTrainerId = null;
        MapStatus = MapStatus.Done;
    }

    public void ReloadMapAfterBattle() => MapStatus = MapStatus.Start;

    // -----------------------------------------------------------------------
    // IScriptContext — Text / UI (rendering delegated to L6)
    // -----------------------------------------------------------------------

    public void OpenText() { }
    public void CloseText() { }
    public void WriteText(string textId) => PendingText = textId;
    public bool YesOrNo() => PendingYesNoResult;
    public void LoadMenu(string menuId) => PendingMenu = menuId;
    public void CloseWindow() { }

    // -----------------------------------------------------------------------
    // IScriptContext — Misc
    // -----------------------------------------------------------------------

    public byte RandomByte(byte max) => max == 0 ? (byte)0 : (byte)Random.Shared.Next(max);
    public string PlayerName => Player.PlayerName;
    public string RivalName => Player.RivalName;
}

// -----------------------------------------------------------------------
// Supporting value types
// -----------------------------------------------------------------------

public record BattleSetup(string? WildSpeciesId, string? TrainerId, int WildLevel, bool IsWild);
public record GivePokemonRequest(string SpeciesId, int Level, string HeldItemId,
    bool FromTrainer, string? Nickname, string? OtName);
public record MovementRequest(int ObjectId, string MovementScriptId);
public enum MapStatus { Start = 0, Enter = 1, Handle = 2, Done = 3 }
