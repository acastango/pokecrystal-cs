namespace PokeCrystal.Scripting.Commands;

/// <summary>giveitem (0x1F)</summary>
public sealed class GiveItemCommand : IScriptCommand
{
    public byte Opcode => 0x1F;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        // item and quantity encoded as string ID + byte at load time
        string itemId = ReadItemId(reader);
        byte qty = reader.ReadByte();
        if (ctx.BagIsFull(itemId)) { ctx.ScriptVar = 0; return null; }
        ctx.GiveItem(itemId, qty);
        ctx.ScriptVar = 1;
        return null;
    }
    private static string ReadItemId(ScriptReader r) => r.ReadByte().ToString();
}

/// <summary>takeitem (0x20)</summary>
public sealed class TakeItemCommand : IScriptCommand
{
    public byte Opcode => 0x20;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string itemId = reader.ReadByte().ToString();
        byte qty = reader.ReadByte();
        ctx.TakeItem(itemId, qty);
        return null;
    }
}

/// <summary>checkitem (0x21) — ScriptVar = 1 if player has ≥1 of item.</summary>
public sealed class CheckItemCommand : IScriptCommand
{
    public byte Opcode => 0x21;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string itemId = reader.ReadByte().ToString();
        ctx.ScriptVar = ctx.HasItem(itemId) ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>givemoney (0x22)</summary>
public sealed class GiveMoneyCommand : IScriptCommand
{
    public byte Opcode => 0x22;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        int account = reader.ReadByte();
        int amount = reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
        ctx.GiveMoney(account, amount);
        return null;
    }
}

/// <summary>takemoney (0x23)</summary>
public sealed class TakeMoneyCommand : IScriptCommand
{
    public byte Opcode => 0x23;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        int account = reader.ReadByte();
        int amount = reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
        ctx.TakeMoney(account, amount);
        return null;
    }
}

/// <summary>checkmoney (0x24) — ScriptVar = 1 if player has ≥ amount.</summary>
public sealed class CheckMoneyCommand : IScriptCommand
{
    public byte Opcode => 0x24;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        int account = reader.ReadByte();
        int amount = reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
        ctx.ScriptVar = ctx.HasMoney(account, amount) ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>givecoins (0x25)</summary>
public sealed class GiveCoinsCommand : IScriptCommand
{
    public byte Opcode => 0x25;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        int coins = reader.ReadWord();
        ctx.GiveCoins(coins);
        return null;
    }
}

/// <summary>takecoins (0x26)</summary>
public sealed class TakeCoinsCommand : IScriptCommand
{
    public byte Opcode => 0x26;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        int coins = reader.ReadWord();
        ctx.TakeCoins(coins);
        return null;
    }
}

/// <summary>checkcoins (0x27)</summary>
public sealed class CheckCoinsCommand : IScriptCommand
{
    public byte Opcode => 0x27;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        int coins = reader.ReadWord();
        ctx.ScriptVar = ctx.HasCoins(coins) ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>checkevent (0x31)</summary>
public sealed class CheckEventCommand : IScriptCommand
{
    public byte Opcode => 0x31;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string id = reader.ReadScriptId();
        ctx.ScriptVar = ctx.CheckEvent(id) ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>clearevent (0x32)</summary>
public sealed class ClearEventCommand : IScriptCommand
{
    public byte Opcode => 0x32;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ClearEvent(reader.ReadScriptId());
        return null;
    }
}

/// <summary>setevent (0x33)</summary>
public sealed class SetEventCommand : IScriptCommand
{
    public byte Opcode => 0x33;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.SetEvent(reader.ReadScriptId());
        return null;
    }
}

/// <summary>checkflag (0x34)</summary>
public sealed class CheckFlagCommand : IScriptCommand
{
    public byte Opcode => 0x34;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string id = reader.ReadScriptId();
        ctx.ScriptVar = ctx.CheckFlag(id) ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>clearflag (0x35)</summary>
public sealed class ClearFlagCommand : IScriptCommand
{
    public byte Opcode => 0x35;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ClearFlag(reader.ReadScriptId());
        return null;
    }
}

/// <summary>setflag (0x36)</summary>
public sealed class SetFlagCommand : IScriptCommand
{
    public byte Opcode => 0x36;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.SetFlag(reader.ReadScriptId());
        return null;
    }
}

/// <summary>checkscene (0x13) — ScriptVar = current scene id for the current map.</summary>
public sealed class CheckSceneCommand : IScriptCommand
{
    public byte Opcode => 0x13;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        // scene is set on the current map; map ID not needed for the current map
        ctx.ScriptVar = 0; // L6 context overrides this properly
        return null;
    }
}

/// <summary>setscene (0x14)</summary>
public sealed class SetSceneCommand : IScriptCommand
{
    public byte Opcode => 0x14;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte sceneId = reader.ReadByte();
        ctx.SetScene(string.Empty, sceneId); // current map
        return null;
    }
}

/// <summary>checkmapscene (0x11) — ScriptVar = scene for a specific map.</summary>
public sealed class CheckMapSceneCommand : IScriptCommand
{
    public byte Opcode => 0x11;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string mapId = reader.ReadScriptId();
        ctx.ScriptVar = (byte)ctx.GetScene(mapId);
        return null;
    }
}

/// <summary>setmapscene (0x12)</summary>
public sealed class SetMapSceneCommand : IScriptCommand
{
    public byte Opcode => 0x12;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string mapId = reader.ReadScriptId();
        byte sceneId = reader.ReadByte();
        ctx.SetScene(mapId, sceneId);
        return null;
    }
}

/// <summary>warp (0x3C)</summary>
public sealed class WarpCommand : IScriptCommand
{
    public byte Opcode => 0x3C;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string mapId = reader.ReadScriptId();
        byte warpId = reader.ReadByte();
        ctx.Warp(mapId, warpId);
        return null;
    }
}

/// <summary>addcellnum (0x28)</summary>
public sealed class AddCellNumCommand : IScriptCommand
{
    public byte Opcode => 0x28;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string contactId = reader.ReadByte().ToString();
        ctx.AddPhoneNumber(contactId);
        return null;
    }
}

/// <summary>delcellnum (0x29)</summary>
public sealed class DelCellNumCommand : IScriptCommand
{
    public byte Opcode => 0x29;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string contactId = reader.ReadByte().ToString();
        ctx.DeletePhoneNumber(contactId);
        return null;
    }
}

/// <summary>checkcellnum (0x2A)</summary>
public sealed class CheckCellNumCommand : IScriptCommand
{
    public byte Opcode => 0x2A;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string contactId = reader.ReadByte().ToString();
        ctx.ScriptVar = ctx.HasPhoneNumber(contactId) ? (byte)1 : (byte)0;
        return null;
    }
}
