namespace PokeCrystal.Scripting;

using PokeCrystal.Schema;

/// <summary>
/// World interface exposed to script commands and specials.
/// Implemented by the Game layer (L6); the Scripting layer only depends on this interface.
/// Mirrors the wScriptVar / wScriptMode state and game-world side effects from Crystal's script VM.
/// </summary>
public interface IScriptContext
{
    // --- VM state ---

    byte ScriptVar { get; set; }        // wScriptVar: single-byte accumulator for check results
    ScriptMode Mode { get; set; }
    int WaitDelay { get; set; }         // wScriptDelay countdown
    bool IsMovementComplete { get; }    // SCRIPTED_MOVEMENT_STATE_F cleared

    // --- Inventory ---

    bool HasItem(string itemId, int quantity = 1);
    void GiveItem(string itemId, int quantity);
    void TakeItem(string itemId, int quantity);
    bool BagIsFull(string itemId);

    // --- Money / Coins ---

    int GetMoney(int account);      // account 0 = player wallet
    void GiveMoney(int account, int amount);
    void TakeMoney(int account, int amount);
    bool HasMoney(int account, int amount);
    int GetCoins();
    void GiveCoins(int amount);
    void TakeCoins(int amount);
    bool HasCoins(int amount);

    // --- Party ---

    bool HasPokemon(string speciesId);
    void GivePokemon(string speciesId, int level, string heldItemId, bool fromTrainer,
        string? nickname, string? otName);
    void GiveEgg(string speciesId);

    // --- Events / Flags / Scenes ---

    bool CheckEvent(string eventId);
    void SetEvent(string eventId);
    void ClearEvent(string eventId);
    bool CheckFlag(string flagId);
    void SetFlag(string flagId);
    void ClearFlag(string flagId);
    int GetScene(string mapId);
    void SetScene(string mapId, int sceneId);

    // --- Phone ---

    bool HasPhoneNumber(string contactId);
    void AddPhoneNumber(string contactId);
    void DeletePhoneNumber(string contactId);

    // --- World ---

    TimeOfDay CurrentTimeOfDay { get; }
    void Warp(string mapId, int warpId);
    void ApplyMovement(int objectId, string movementScriptId);
    void FacePlayer(int objectId);
    void SetObjectVisible(int objectId, bool visible);
    void PlayMusic(string musicId);
    void PlaySound(string soundId);
    void WaitSfx();

    // --- Battle ---

    void LoadWildMon(string speciesId, int level);
    void LoadTrainer(string trainerId);
    void StartBattle();
    void ReloadMapAfterBattle();

    // --- Text / UI ---

    void OpenText();
    void CloseText();
    void WriteText(string textId);
    bool YesOrNo();
    void LoadMenu(string menuId);
    void CloseWindow();

    // --- Misc ---

    byte RandomByte(byte max);
    string PlayerName { get; }
    string RivalName { get; }
}

public enum ScriptMode { End = 0, Read = 1, WaitMovement = 2, Wait = 3 }
