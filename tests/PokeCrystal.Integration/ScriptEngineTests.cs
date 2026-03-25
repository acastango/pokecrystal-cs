namespace PokeCrystal.Integration;

using System;
using System.Collections.Generic;
using PokeCrystal.Scripting;
using PokeCrystal.Scripting.Commands;
using PokeCrystal.Scripting.Specials;
using PokeCrystal.Schema;
using Xunit;

/// <summary>
/// ScriptEngine integration tests.
/// Builds the engine the same way ScriptingRegistry.AddCrystalScripting() does,
/// registers minimal inline scripts, and exercises the Run() loop.
/// Opcode values from Commands/ControlFlowCommands.cs.
/// </summary>
public sealed class ScriptEngineTests
{
    private static ScriptEngine BuildEngine(ScriptRegistry scriptRegistry)
    {
        var specials = new SpecialRegistry();
        // Mirror ScriptingRegistry.BuildBaseCommands + post-construction registration.
        var cmds = new Dictionary<byte, IScriptCommand>();
        void Add(IScriptCommand c) => cmds[c.Opcode] = c;

        Add(new ScallCommand());
        Add(new SjumpCommand());
        Add(new WaitCommand());
        Add(new PauseCommand());
        // More commands would be added by ScriptingRegistry; we need at minimum
        // the END family for these tests.

        var engine = new ScriptEngine(cmds, scriptRegistry, specials);
        engine.RegisterCommand(new EndCallbackCommand(engine));  // 0x90
        engine.RegisterCommand(new EndCommand(engine));          // 0x91
        engine.RegisterCommand(new EndAllCommand(engine));       // 0x93
        return engine;
    }

    private static IScriptContext MakeCtx() => new MinimalScriptContext();

    // -----------------------------------------------------------------------
    // END (0x91) — terminates current script; Mode becomes End.
    // -----------------------------------------------------------------------

    [Fact]
    public void End_opcode_sets_mode_to_End()
    {
        var registry = new ScriptRegistry();
        registry.Register("test_end", new byte[] { 0x91 }); // END

        var engine = BuildEngine(registry);
        var ctx = MakeCtx();

        engine.Start("test_end", ctx);
        Assert.Equal(ScriptMode.Read, ctx.Mode);

        engine.Run(ctx);
        Assert.Equal(ScriptMode.End, ctx.Mode);
    }

    // -----------------------------------------------------------------------
    // ENDCALLBACK (0x90) — return from callee; with empty stack also sets End.
    // -----------------------------------------------------------------------

    [Fact]
    public void EndCallback_with_empty_stack_sets_End()
    {
        var registry = new ScriptRegistry();
        registry.Register("test_endcallback", new byte[] { 0x90 }); // ENDCALLBACK

        var engine = BuildEngine(registry);
        var ctx = MakeCtx();

        engine.Start("test_endcallback", ctx);
        engine.Run(ctx);
        Assert.Equal(ScriptMode.End, ctx.Mode);
    }

    // -----------------------------------------------------------------------
    // SCALL (0x00) + ENDCALLBACK (0x90) — call/return roundtrip.
    // Caller: SCALL "callee", ENDALL
    // Callee: ENDCALLBACK
    // Expected: callee returns to caller, caller hits ENDALL → Mode.End
    // -----------------------------------------------------------------------

    [Fact]
    public void Scall_and_EndCallback_return_to_caller()
    {
        var registry = new ScriptRegistry();

        // Callee registered under numeric key "1" (ushort 0x0001).
        // ScriptReader.ReadScriptId() reads a 16-bit LE word and returns idx.ToString().
        registry.Register("1", new byte[] { 0x90 }); // ENDCALLBACK

        // Caller: SCALL 1 + END
        // SCALL opcode 0x00, followed by ushort 1 little-endian [0x01, 0x00].
        registry.Register("caller", new byte[] { 0x00, 0x01, 0x00, 0x91 });

        var engine = BuildEngine(registry);
        var ctx = MakeCtx();

        engine.Start("caller", ctx);
        engine.Run(ctx);
        Assert.Equal(ScriptMode.End, ctx.Mode);
    }

    // -----------------------------------------------------------------------
    // Empty script (zero bytes) — reader hits end immediately → Mode.End.
    // -----------------------------------------------------------------------

    [Fact]
    public void Empty_script_ends_without_crash()
    {
        var registry = new ScriptRegistry();
        registry.Register("empty", Array.Empty<byte>());

        var engine = BuildEngine(registry);
        var ctx = MakeCtx();

        engine.Start("empty", ctx);
        engine.Step(ctx); // first Step: reader.IsEnd → sets Mode.End
        Assert.Equal(ScriptMode.End, ctx.Mode);
    }

    // -----------------------------------------------------------------------
    // WAIT (0xA8, 1 byte delay) — suspends then resumes.
    // -----------------------------------------------------------------------

    [Fact]
    public void Wait_suspends_then_resumes_after_countdown()
    {
        var registry = new ScriptRegistry();
        // WAIT 3, END
        registry.Register("test_wait", new byte[] { 0xA8, 3, 0x91 });

        var engine = BuildEngine(registry);
        var ctx = MakeCtx();

        engine.Start("test_wait", ctx);

        engine.Step(ctx); // executes WAIT — sets Mode.Wait, WaitDelay=3
        Assert.Equal(ScriptMode.Wait, ctx.Mode);

        engine.Step(ctx); // WaitDelay 3→2
        engine.Step(ctx); // WaitDelay 2→1
        engine.Step(ctx); // WaitDelay 1→0 → Mode.Read
        engine.Step(ctx); // executes END → Mode.End
        Assert.Equal(ScriptMode.End, ctx.Mode);
    }

    // -----------------------------------------------------------------------
    // Minimal IScriptContext for scripting tests (no World dependencies).
    // -----------------------------------------------------------------------

    private sealed class MinimalScriptContext : IScriptContext
    {
        public byte ScriptVar { get; set; }
        public ScriptMode Mode { get; set; }
        public int WaitDelay { get; set; }
        public bool IsMovementComplete => true;

        // --- Inventory ---
        public bool HasItem(string itemId, int quantity = 1) => false;
        public void GiveItem(string itemId, int quantity) { }
        public void TakeItem(string itemId, int quantity) { }
        public bool BagIsFull(string itemId) => false;

        // --- Money / Coins ---
        public int GetMoney(int account) => 0;
        public void GiveMoney(int account, int amount) { }
        public void TakeMoney(int account, int amount) { }
        public bool HasMoney(int account, int amount) => false;
        public int GetCoins() => 0;
        public void GiveCoins(int amount) { }
        public void TakeCoins(int amount) { }
        public bool HasCoins(int amount) => false;

        // --- Pokémon ---
        public bool HasPokemon(string speciesId) => false;
        public void GivePokemon(string speciesId, int level, string heldItemId, bool fromTrainer, string? nickname, string? otName) { }
        public void GiveEgg(string speciesId) { }

        // --- Events / Flags / Scenes ---
        public bool CheckEvent(string eventId) => false;
        public void SetEvent(string eventId) { }
        public void ClearEvent(string eventId) { }
        public bool CheckFlag(string flagId) => false;
        public void SetFlag(string flagId) { }
        public void ClearFlag(string flagId) { }
        public int GetScene(string mapId) => 0;
        public void SetScene(string mapId, int sceneId) { }

        // --- Phone ---
        public bool HasPhoneNumber(string contactId) => false;
        public void AddPhoneNumber(string contactId) { }
        public void DeletePhoneNumber(string contactId) { }

        // --- World ---
        public TimeOfDay CurrentTimeOfDay => TimeOfDay.Day;
        public void Warp(string mapId, int warpId) { }
        public void ApplyMovement(int objectId, string movementScriptId) { }
        public void FacePlayer(int objectId) { }
        public void SetObjectVisible(int objectId, bool visible) { }
        public void PlayMusic(string musicId) { }
        public void PlaySound(string soundId) { }
        public void WaitSfx() { }

        // --- Battle ---
        public void LoadWildMon(string speciesId, int level) { }
        public void LoadTrainer(string trainerId) { }
        public void StartBattle() { }
        public void ReloadMapAfterBattle() { }

        // --- Text / UI ---
        public void OpenText() { }
        public void CloseText() { }
        public void WriteText(string textId) { }
        public bool YesOrNo() => true;
        public void LoadMenu(string menuId) { }
        public void CloseWindow() { }

        // --- Misc ---
        public byte RandomByte(byte max) => 0;
        public string PlayerName => "RED";
        public string RivalName => "BLUE";
    }
}
