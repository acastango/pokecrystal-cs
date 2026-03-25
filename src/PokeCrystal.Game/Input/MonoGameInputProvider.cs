namespace PokeCrystal.Game.Input;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeCrystal.Schema;

/// <summary>
/// IInputProvider backed by MonoGame's Keyboard and GamePad APIs.
/// Default bindings: WASD / arrows = movement, Z / Enter / Space = Confirm,
/// X / Escape = Cancel, Escape = Menu.
/// </summary>
public sealed class MonoGameInputProvider : IInputProvider
{
    private KeyboardState _prev;
    private KeyboardState _curr;

    // Default keyboard mapping
    private static readonly Dictionary<GameAction, Keys[]> DefaultBindings = new()
    {
        [GameAction.MoveUp]        = [Keys.W, Keys.Up],
        [GameAction.MoveDown]      = [Keys.S, Keys.Down],
        [GameAction.MoveLeft]      = [Keys.A, Keys.Left],
        [GameAction.MoveRight]     = [Keys.D, Keys.Right],
        [GameAction.Confirm]       = [Keys.Z, Keys.Enter, Keys.Space],
        [GameAction.Cancel]        = [Keys.X, Keys.Back],
        [GameAction.Menu]          = [Keys.Escape, Keys.Enter],
        [GameAction.RegisteredItem]= [Keys.Q],
        [GameAction.SpeedToggle]   = [Keys.Tab],
        [GameAction.QuickSave]     = [Keys.F5],
        [GameAction.QuickLoad]     = [Keys.F9],
        [GameAction.DebugConsole]  = [Keys.OemTilde],
    };

    private readonly Dictionary<string, Keys[]> _customBindings = new(StringComparer.Ordinal);

    public void Update()
    {
        _prev = _curr;
        _curr = Keyboard.GetState();
    }

    // --- GameAction overloads ---

    public bool IsPressed(GameAction action)
    {
        if (!DefaultBindings.TryGetValue(action, out var keys)) return false;
        return keys.Any(k => _curr.IsKeyDown(k) && _prev.IsKeyUp(k));
    }

    public bool IsHeld(GameAction action)
    {
        if (!DefaultBindings.TryGetValue(action, out var keys)) return false;
        return keys.Any(k => _curr.IsKeyDown(k));
    }

    public bool IsReleased(GameAction action)
    {
        if (!DefaultBindings.TryGetValue(action, out var keys)) return false;
        return keys.Any(k => _curr.IsKeyUp(k) && _prev.IsKeyDown(k));
    }

    // --- Custom action overloads ---

    public void RegisterAction(string actionKey) => _customBindings.TryAdd(actionKey, []);

    public bool IsPressed(string actionKey)
    {
        if (!_customBindings.TryGetValue(actionKey, out var keys)) return false;
        return keys.Any(k => _curr.IsKeyDown(k) && _prev.IsKeyUp(k));
    }

    public bool IsHeld(string actionKey)
    {
        if (!_customBindings.TryGetValue(actionKey, out var keys)) return false;
        return keys.Any(k => _curr.IsKeyDown(k));
    }

    // --- Analog / mouse (stubs — gamepad support can be added here) ---

    public (float X, float Y) GetAnalogDirection()
    {
        float x = IsHeld(GameAction.MoveRight) ? 1f : IsHeld(GameAction.MoveLeft) ? -1f : 0f;
        float y = IsHeld(GameAction.MoveDown)  ? 1f : IsHeld(GameAction.MoveUp)   ? -1f : 0f;
        return (x, y);
    }

    public (int X, int Y) GetMousePosition()
    {
        var ms = Mouse.GetState();
        return (ms.X, ms.Y);
    }
}
