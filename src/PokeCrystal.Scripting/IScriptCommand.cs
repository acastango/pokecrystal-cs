namespace PokeCrystal.Scripting;

/// <summary>
/// A single script command handler (one opcode → one implementation).
/// </summary>
public interface IScriptCommand
{
    byte Opcode { get; }

    /// <summary>
    /// Execute this command. Reads operand bytes from <paramref name="reader"/> as needed.
    /// May modify ctx.Mode to change execution state (e.g. end, wait).
    /// Returns the script name to jump/call into, or null to continue sequentially.
    /// </summary>
    ScriptJump? Execute(ScriptReader reader, IScriptContext ctx);
}

/// <summary>Describes a control-flow transition from a command.</summary>
public record ScriptJump(string TargetId, bool IsCall);
