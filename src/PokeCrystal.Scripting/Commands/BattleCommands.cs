namespace PokeCrystal.Scripting.Commands;

/// <summary>loadwildmon (0x5D)</summary>
public sealed class LoadWildMonCommand : IScriptCommand
{
    public byte Opcode => 0x5D;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string speciesId = reader.ReadByte().ToString();
        byte level = reader.ReadByte();
        ctx.LoadWildMon(speciesId, level);
        return null;
    }
}

/// <summary>loadtrainer (0x5E)</summary>
public sealed class LoadTrainerCommand : IScriptCommand
{
    public byte Opcode => 0x5E;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string trainerId = reader.ReadScriptId();
        ctx.LoadTrainer(trainerId);
        return null;
    }
}

/// <summary>startbattle (0x5F)</summary>
public sealed class StartBattleCommand : IScriptCommand
{
    public byte Opcode => 0x5F;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.StartBattle();
        ctx.Mode = ScriptMode.WaitMovement; // wait for battle to complete
        return null;
    }
}

/// <summary>reloadmapafterbattle (0x60)</summary>
public sealed class ReloadMapAfterBattleCommand : IScriptCommand
{
    public byte Opcode => 0x60;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.ReloadMapAfterBattle();
        return null;
    }
}
