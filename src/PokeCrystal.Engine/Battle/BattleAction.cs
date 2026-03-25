namespace PokeCrystal.Engine.Battle;

/// <summary>Choice made by the player for one turn.</summary>
public abstract record BattleAction;

/// <summary>Use move at slot index 0-3. Engine converts to Struggle when PP is exhausted.</summary>
public record UseMoveAction(int SlotIndex) : BattleAction;

/// <summary>Attempt to flee (wild battles only).</summary>
public record FleeAction : BattleAction;
