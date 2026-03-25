namespace PokeCrystal.Scripting;

/// <summary>
/// Sequential byte reader over a script's instruction stream.
/// Mirrors Crystal's GetScriptByte — advances a position pointer on each read.
/// </summary>
public sealed class ScriptReader
{
    private readonly ReadOnlyMemory<byte> _bytes;
    private int _pos;

    public ScriptReader(ReadOnlyMemory<byte> bytes, int startPos = 0)
    {
        _bytes = bytes;
        _pos = startPos;
    }

    public int Position => _pos;
    public bool IsEnd => _pos >= _bytes.Length;

    public byte ReadByte()
    {
        if (_pos >= _bytes.Length)
            throw new InvalidOperationException("Script reader past end of stream.");
        return _bytes.Span[_pos++];
    }

    public ushort ReadWord()
    {
        byte lo = ReadByte();
        byte hi = ReadByte();
        return (ushort)(lo | (hi << 8));
    }

    public string ReadScriptId()
    {
        // Scripts encode their target as a 16-bit index into a name table.
        // At runtime, callers resolve the ushort to a registered script name.
        ushort idx = ReadWord();
        return idx.ToString(); // resolved to string name by ScriptRegistry at load time
    }

    public void Seek(int pos) => _pos = pos;
}
