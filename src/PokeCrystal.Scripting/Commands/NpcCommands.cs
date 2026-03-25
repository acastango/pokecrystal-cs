namespace PokeCrystal.Scripting.Commands;

/// <summary>applymovement (0x69)</summary>
public sealed class ApplyMovementCommand : IScriptCommand
{
    public byte Opcode => 0x69;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte objectId = reader.ReadByte();
        string movementId = reader.ReadScriptId();
        ctx.ApplyMovement(objectId, movementId);
        ctx.Mode = ScriptMode.WaitMovement;
        return null;
    }
}

/// <summary>applymovementlasttalked (0x6A)</summary>
public sealed class ApplyMovementLastTalkedCommand : IScriptCommand
{
    public byte Opcode => 0x6A;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string movementId = reader.ReadScriptId();
        ctx.ApplyMovement(-1, movementId); // -1 = last talked object
        ctx.Mode = ScriptMode.WaitMovement;
        return null;
    }
}

/// <summary>faceplayer (0x6B)</summary>
public sealed class FacePlayerCommand : IScriptCommand
{
    public byte Opcode => 0x6B;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        ctx.FacePlayer(-1); // last talked NPC faces player
        return null;
    }
}

/// <summary>appear (0x6F)</summary>
public sealed class AppearCommand : IScriptCommand
{
    public byte Opcode => 0x6F;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte objectId = reader.ReadByte();
        ctx.SetObjectVisible(objectId, true);
        return null;
    }
}

/// <summary>disappear (0x6E)</summary>
public sealed class DisappearCommand : IScriptCommand
{
    public byte Opcode => 0x6E;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        byte objectId = reader.ReadByte();
        ctx.SetObjectVisible(objectId, false);
        return null;
    }
}
