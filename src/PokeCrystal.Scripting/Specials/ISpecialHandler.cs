namespace PokeCrystal.Scripting.Specials;

/// <summary>
/// Named special — a C# method invokable from a script via the `special` command.
/// Mirrors Crystal's SpecialsPointers table dispatch (engine/events/specials.asm).
/// Input/output goes through ctx.ScriptVar (byte).
/// </summary>
public interface ISpecialHandler
{
    string Key { get; }
    void Execute(IScriptContext ctx);
}
