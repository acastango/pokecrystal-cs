namespace PokeCrystal.Scripting.Commands;

/// <summary>
/// scall (0x00) — call a script by name, push return address.
/// </summary>
public sealed class ScallCommand : IScriptCommand
{
    public byte Opcode => 0x00;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
        => new(reader.ReadScriptId(), IsCall: true);
}

/// <summary>
/// sjump (0x03) — jump to a script (no return).
/// </summary>
public sealed class SjumpCommand : IScriptCommand
{
    public byte Opcode => 0x03;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
        => new(reader.ReadScriptId(), IsCall: false);
}

/// <summary>
/// ifequal (0x06) — jump if ScriptVar == value.
/// </summary>
public sealed class IfEqualCommand : IScriptCommand
{
    public byte Opcode => 0x06;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte value = reader.ReadByte();
        string target = reader.ReadScriptId();
        return ctx.ScriptVar == value ? new(target, IsCall: false) : null;
    }
}

/// <summary>
/// ifnotequal (0x07) — jump if ScriptVar != value.
/// </summary>
public sealed class IfNotEqualCommand : IScriptCommand
{
    public byte Opcode => 0x07;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte value = reader.ReadByte();
        string target = reader.ReadScriptId();
        return ctx.ScriptVar != value ? new(target, IsCall: false) : null;
    }
}

/// <summary>
/// iffalse (0x08) — jump if ScriptVar == 0.
/// </summary>
public sealed class IfFalseCommand : IScriptCommand
{
    public byte Opcode => 0x08;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string target = reader.ReadScriptId();
        return ctx.ScriptVar == 0 ? new(target, IsCall: false) : null;
    }
}

/// <summary>
/// iftrue (0x09) — jump if ScriptVar != 0.
/// </summary>
public sealed class IfTrueCommand : IScriptCommand
{
    public byte Opcode => 0x09;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string target = reader.ReadScriptId();
        return ctx.ScriptVar != 0 ? new(target, IsCall: false) : null;
    }
}

/// <summary>
/// ifgreater (0x0A) — jump if ScriptVar > value.
/// </summary>
public sealed class IfGreaterCommand : IScriptCommand
{
    public byte Opcode => 0x0A;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte value = reader.ReadByte();
        string target = reader.ReadScriptId();
        return ctx.ScriptVar > value ? new(target, IsCall: false) : null;
    }
}

/// <summary>
/// ifless (0x0B) — jump if ScriptVar &lt; value.
/// </summary>
public sealed class IfLessCommand : IScriptCommand
{
    public byte Opcode => 0x0B;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte value = reader.ReadByte();
        string target = reader.ReadScriptId();
        return ctx.ScriptVar < value ? new(target, IsCall: false) : null;
    }
}

/// <summary>
/// jumpstd (0x0C) — jump to a StdScript by index (resolved to name at load time).
/// </summary>
public sealed class JumpStdCommand : IScriptCommand
{
    public byte Opcode => 0x0C;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
        => new(reader.ReadScriptId(), IsCall: false);
}

/// <summary>
/// callstd (0x0D) — call a StdScript by index (resolved to name at load time).
/// </summary>
public sealed class CallStdCommand : IScriptCommand
{
    public byte Opcode => 0x0D;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
        => new(reader.ReadScriptId(), IsCall: true);
}

/// <summary>
/// end (0x91) — terminate the current script entirely.
/// </summary>
public sealed class EndCommand(ScriptEngine engine) : IScriptCommand
{
    public byte Opcode => 0x91;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        engine.End(ctx);
        return null;
    }
}

/// <summary>
/// endcallback (0x90) — return from a called script to the caller.
/// </summary>
public sealed class EndCallbackCommand(ScriptEngine engine) : IScriptCommand
{
    public byte Opcode => 0x90;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        engine.Return(ctx);
        return null;
    }
}

/// <summary>
/// endall (0x93) — clear call stack and end.
/// </summary>
public sealed class EndAllCommand(ScriptEngine engine) : IScriptCommand
{
    public byte Opcode => 0x93;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        engine.End(ctx);
        return null;
    }
}

/// <summary>
/// wait (0xA8) — pause execution for n frames (wScriptDelay).
/// </summary>
public sealed class WaitCommand : IScriptCommand
{
    public byte Opcode => 0xA8;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.WaitDelay = reader.ReadByte();
        ctx.Mode = ScriptMode.Wait;
        return null;
    }
}

/// <summary>
/// pause (0x8B) — pause for a fixed delay (reads 1-byte count).
/// </summary>
public sealed class PauseCommand : IScriptCommand
{
    public byte Opcode => 0x8B;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.WaitDelay = reader.ReadByte();
        ctx.Mode = ScriptMode.Wait;
        return null;
    }
}
