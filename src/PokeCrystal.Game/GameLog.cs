namespace PokeCrystal.Game;

/// <summary>
/// Lightweight append-only file logger for in-game diagnostics.
/// Writes to game_debug.log next to the executable.
/// Add/remove categories by toggling the Enable* flags.
/// </summary>
public static class GameLog
{
    public static bool EnableMovement { get; set; } = true;

    private static readonly string _path =
        Path.Combine(AppContext.BaseDirectory, "game_debug.log");

    private static long _frame;

    /// <summary>Call once per game Update to increment the frame counter.</summary>
    public static void Tick() => _frame++;

    /// <summary>Append one line to the log file (no-op if category is disabled).</summary>
    public static void Write(string message)
    {
        try { File.AppendAllText(_path, $"[f{_frame:D6}] {message}\n"); }
        catch { /* swallow I/O errors — never crash the game */ }
    }

    /// <summary>Delete the log file and reset the frame counter (call on game start).</summary>
    public static void Reset()
    {
        _frame = 0;
        try { File.Delete(_path); } catch { }
    }
}
