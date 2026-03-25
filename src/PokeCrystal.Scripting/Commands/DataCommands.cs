namespace PokeCrystal.Scripting.Commands;

/// <summary>setval (0x15) — set ScriptVar to a literal byte.</summary>
public sealed class SetValCommand : IScriptCommand
{
    public byte Opcode => 0x15;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ScriptVar = reader.ReadByte();
        return null;
    }
}

/// <summary>addval (0x16) — add a literal byte to ScriptVar (wrapping).</summary>
public sealed class AddValCommand : IScriptCommand
{
    public byte Opcode => 0x16;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ScriptVar = (byte)(ctx.ScriptVar + reader.ReadByte());
        return null;
    }
}

/// <summary>random (0x17) — set ScriptVar to random byte in [0, max).</summary>
public sealed class RandomCommand : IScriptCommand
{
    public byte Opcode => 0x17;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte max = reader.ReadByte();
        ctx.ScriptVar = ctx.RandomByte(max);
        return null;
    }
}

/// <summary>checktime (0x2B) — set ScriptVar to 1 if current time matches the given slot.</summary>
public sealed class CheckTimeCommand : IScriptCommand
{
    public byte Opcode => 0x2B;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        // time slot byte: 0=morning, 1=day, 2=night (simplified mapping)
        byte slot = reader.ReadByte();
        var expected = slot switch
        {
            0 => Schema.TimeOfDay.Morning,
            1 => Schema.TimeOfDay.Day,
            _ => Schema.TimeOfDay.Night,
        };
        ctx.ScriptVar = ctx.CurrentTimeOfDay == expected ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>checkver (0x18) — sets ScriptVar to 0 (Gold) or 1 (Crystal). Always 1 in C#.</summary>
public sealed class CheckVerCommand : IScriptCommand
{
    public byte Opcode => 0x18;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ScriptVar = 1; // Crystal
        return null;
    }
}
