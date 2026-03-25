namespace PokeCrystal.Game;

using System.Text;

/// <summary>
/// In-game debug console. Toggle with ~ (OemTilde).
/// Commands are registered as named delegates so any system can expose them.
/// </summary>
public sealed class DebugConsole
{
    private readonly StringBuilder _input = new();
    private readonly Dictionary<string, Func<string[], string>> _commands = new(StringComparer.OrdinalIgnoreCase);

    public bool IsOpen { get; private set; }
    public string CurrentInput => _input.ToString();
    public string LastOutput { get; private set; } = string.Empty;

    /// <summary>Register a command. Handler receives args[0..n-1] (excluding the command name).</summary>
    public void Register(string name, Func<string[], string> handler)
        => _commands[name] = handler;

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (!IsOpen) return;
        _input.Clear();
        LastOutput = string.Empty;
    }

    /// <summary>
    /// Feed a character from Window.TextInput.
    /// '\b' = backspace, '\r' = submit.
    /// Other control chars are ignored.
    /// </summary>
    public void Feed(char c)
    {
        if (!IsOpen) return;

        if (c == '\r' || c == '\n')
        {
            Execute(_input.ToString().Trim());
            _input.Clear();
            return;
        }

        if (c == '\b')
        {
            if (_input.Length > 0)
                _input.Remove(_input.Length - 1, 1);
            return;
        }

        if (char.IsControl(c)) return;
        _input.Append(c);
    }

    private void Execute(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0];
        var args = parts.Length > 1 ? parts[1..] : [];

        if (_commands.TryGetValue(cmd, out var handler))
        {
            try   { LastOutput = handler(args); }
            catch (Exception ex) { LastOutput = $"Error: {ex.Message}"; }
        }
        else
        {
            LastOutput = $"Unknown command: {cmd}";
        }
    }
}
