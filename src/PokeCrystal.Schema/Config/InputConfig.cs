namespace PokeCrystal.Schema;

/// <summary>
/// Abstract game actions. Game logic references only these — never physical keys.
/// Mods register new actions as string keys via IInputProvider.RegisterAction.
/// </summary>
public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Confirm,
    Cancel,
    Menu,
    RegisteredItem,
    SpeedToggle,
    QuickSave,
    QuickLoad,
    DebugConsole,
}

public enum InputDevice { Keyboard, Gamepad, Mouse }

/// <summary>A single physical input source (device + key/button name).</summary>
public record InputSource(InputDevice Device, string Key);

/// <summary>Binding of a GameAction to one or more physical sources.</summary>
public record InputBinding(GameAction Action, List<InputSource> Sources);

/// <summary>Full input configuration — loaded from settings, rebindable by player.</summary>
public record InputConfig(List<InputBinding> Bindings);
