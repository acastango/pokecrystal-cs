namespace PokeCrystal.Schema;

/// <summary>
/// Abstracts physical input from game logic. Game code queries GameAction values only —
/// never physical keys. Multiple input sources per action are supported.
/// L6 (Game Shell) provides the MonoGame implementation.
/// Mods register new action keys via RegisterAction.
/// </summary>
public interface IInputProvider
{
    bool IsPressed(GameAction action);   // true on the first frame the action is active
    bool IsHeld(GameAction action);      // true while action is continuously held
    bool IsReleased(GameAction action);  // true on the frame the action is released

    (float X, float Y) GetAnalogDirection();
    (int X, int Y) GetMousePosition();

    void RegisterAction(string actionKey);
    bool IsPressed(string actionKey);
    bool IsHeld(string actionKey);
}
