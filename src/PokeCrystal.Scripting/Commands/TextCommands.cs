namespace PokeCrystal.Scripting.Commands;

/// <summary>opentext (0x47)</summary>
public sealed class OpenTextCommand : IScriptCommand
{
    public byte Opcode => 0x47;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx) { ctx.OpenText(); return null; }
}

/// <summary>closetext (0x49)</summary>
public sealed class CloseTextCommand : IScriptCommand
{
    public byte Opcode => 0x49;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx) { ctx.CloseText(); return null; }
}

/// <summary>writetext (0x4C)</summary>
public sealed class WriteTextCommand : IScriptCommand
{
    public byte Opcode => 0x4C;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.WriteText(reader.ReadScriptId());
        return null;
    }
}

/// <summary>farwritetext (0x4B)</summary>
public sealed class FarWriteTextCommand : IScriptCommand
{
    public byte Opcode => 0x4B;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.WriteText(reader.ReadScriptId());
        return null;
    }
}

/// <summary>yesorno (0x4E) — ScriptVar = 1 (yes) or 0 (no).</summary>
public sealed class YesOrNoCommand : IScriptCommand
{
    public byte Opcode => 0x4E;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ScriptVar = ctx.YesOrNo() ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>closewindow (0x50)</summary>
public sealed class CloseWindowCommand : IScriptCommand
{
    public byte Opcode => 0x50;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx) { ctx.CloseWindow(); return null; }
}

/// <summary>waitbutton (0x54)</summary>
public sealed class WaitButtonCommand : IScriptCommand
{
    public byte Opcode => 0x54;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.Mode = ScriptMode.Wait;
        ctx.WaitDelay = 1; // L6 overrides to wait for button press
        return null;
    }
}
