namespace PokeCrystal.Scripting.Commands;

using PokeCrystal.Scripting.Specials;

/// <summary>
/// special (0x0F) — dispatch to a named special handler.
/// The script stream encodes the special as a 16-bit index resolved to a name at load time.
/// </summary>
public sealed class SpecialCommand(SpecialRegistry specials) : IScriptCommand
{
    public byte Opcode => 0x0F;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string key = reader.ReadScriptId(); // index already resolved to name by loader
        specials.Execute(key, ctx);
        return null;
    }
}
