namespace PokeCrystal.Scripting;

using PokeCrystal.Scripting.Specials;

/// <summary>
/// Crystal script VM. Mirrors engine/overworld/scripting.asm ScriptEvents.
///
/// Mode transitions:
///   Read (1)          → fetch opcode byte → dispatch command
///   WaitMovement (2)  → poll ctx.IsMovementComplete → resume when done
///   Wait (3)          → decrement WaitDelay → resume when 0
///   End (0)           → stop
/// </summary>
public sealed class ScriptEngine
{
    private readonly Dictionary<byte, IScriptCommand> _commands;
    private readonly ScriptRegistry _registry;
    private readonly SpecialRegistry _specials;

    // Call stack for scall/farscall return addresses (script name + position)
    private readonly Stack<(string scriptId, int pos)> _callStack = new();

    private string _currentId = string.Empty;
    private ScriptReader _reader = new(ReadOnlyMemory<byte>.Empty);

    public ScriptEngine(
        Dictionary<byte, IScriptCommand> commands,
        ScriptRegistry registry,
        SpecialRegistry specials)
    {
        _commands = commands;
        _registry = registry;
        _specials = specials;
    }

    /// <summary>Register or replace a command handler. Used by the mod system and for
    /// post-construction engine-dependent commands (End, EndCallback, EndAll).</summary>
    public void RegisterCommand(IScriptCommand command) => _commands[command.Opcode] = command;

    /// <summary>Start execution of a named script.</summary>
    public void Start(string scriptId, IScriptContext ctx)
    {
        _callStack.Clear();
        Load(scriptId);
        ctx.Mode = ScriptMode.Read;
    }

    /// <summary>Run until End or until a Wait/WaitMovement suspends execution.</summary>
    public void Run(IScriptContext ctx)
    {
        while (ctx.Mode != ScriptMode.End)
            Step(ctx);
    }

    /// <summary>Advance one iteration of the script loop (call each frame).</summary>
    public void Step(IScriptContext ctx)
    {
        switch (ctx.Mode)
        {
            case ScriptMode.End:
                return;

            case ScriptMode.Read:
                ExecuteCommand(ctx);
                break;

            case ScriptMode.WaitMovement:
                if (ctx.IsMovementComplete)
                    ctx.Mode = ScriptMode.Read;
                break;

            case ScriptMode.Wait:
                ctx.WaitDelay--;
                if (ctx.WaitDelay <= 0)
                    ctx.Mode = ScriptMode.Read;
                break;
        }
    }

    private void ExecuteCommand(IScriptContext ctx)
    {
        if (_reader.IsEnd)
        {
            ctx.Mode = ScriptMode.End;
            return;
        }

        byte opcode = _reader.ReadByte();

        if (!_commands.TryGetValue(opcode, out var cmd))
            throw new InvalidOperationException(
                $"Unknown script opcode 0x{opcode:X2} in '{_currentId}' at pos {_reader.Position - 1}.");

        var jump = cmd.Execute(_reader, ctx);

        if (jump is null) return;

        if (jump.IsCall)
        {
            // Push return address
            _callStack.Push((_currentId, _reader.Position));
        }

        Load(jump.TargetId);
    }

    internal void Return(IScriptContext ctx)
    {
        if (_callStack.Count == 0)
        {
            ctx.Mode = ScriptMode.End;
            return;
        }
        var (id, pos) = _callStack.Pop();
        _currentId = id;
        _reader = new ScriptReader(_registry.Get(id), pos);
    }

    internal void End(IScriptContext ctx)
    {
        _callStack.Clear();
        ctx.Mode = ScriptMode.End;
    }

    internal SpecialRegistry Specials => _specials;

    private void Load(string scriptId)
    {
        _currentId = scriptId;
        _reader = new ScriptReader(_registry.Get(scriptId));
    }
}
