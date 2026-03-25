namespace PokeCrystal.Scripting.Commands;

/// <summary>checkpoke (0x2C) — ScriptVar = 1 if party contains the species.</summary>
public sealed class CheckPokeCommand : IScriptCommand
{
    public byte Opcode => 0x2C;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string speciesId = reader.ReadByte().ToString();
        ctx.ScriptVar = ctx.HasPokemon(speciesId) ? (byte)1 : (byte)0;
        return null;
    }
}

/// <summary>givepoke (0x2D)</summary>
public sealed class GivePokeCommand : IScriptCommand
{
    public byte Opcode => 0x2D;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string speciesId = reader.ReadByte().ToString();
        byte level = reader.ReadByte();
        string itemId = reader.ReadByte().ToString();
        bool fromTrainer = reader.ReadByte() != 0;
        string? nickname = null;
        string? otName = null;
        if (fromTrainer)
        {
            nickname = reader.ReadScriptId();
            otName = reader.ReadScriptId();
        }
        ctx.GivePokemon(speciesId, level, itemId, fromTrainer, nickname, otName);
        return null;
    }
}

/// <summary>giveegg (0x2E)</summary>
public sealed class GiveEggCommand : IScriptCommand
{
    public byte Opcode => 0x2E;
    public ScriptJump? Execute(ScriptReader reader, IScriptContext ctx)
    {
        string speciesId = reader.ReadByte().ToString();
        ctx.GiveEgg(speciesId);
        return null;
    }
}
