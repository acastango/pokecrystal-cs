namespace PokeCrystal.Scripting.Commands;

/// <summary>playmusic (0x7F)</summary>
public sealed class PlayMusicCommand : IScriptCommand
{
    public byte Opcode => 0x7F;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.PlayMusic(reader.ReadScriptId());
        return null;
    }
}

/// <summary>cry (0x84)</summary>
public sealed class CryCommand : IScriptCommand
{
    public byte Opcode => 0x84;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.PlaySound(reader.ReadByte().ToString()); // species ID as cry key
        return null;
    }
}

/// <summary>playsound (0x85)</summary>
public sealed class PlaySoundCommand : IScriptCommand
{
    public byte Opcode => 0x85;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.PlaySound(reader.ReadScriptId());
        return null;
    }
}

/// <summary>waitsfx (0x86)</summary>
public sealed class WaitSfxCommand : IScriptCommand
{
    public byte Opcode => 0x86;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.WaitSfx();
        return null;
    }
}
