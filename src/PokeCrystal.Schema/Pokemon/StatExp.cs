namespace PokeCrystal.Schema;

/// <summary>
/// Gen 2 Stat Experience (EVs). One u16 per stat including the split Special.
/// SpAtk and SpDef share the same StatExp value (wStatExp) in the ASM —
/// both are set from it on party/battle stat recalculation.
/// </summary>
public record StatExp(int Hp, int Attack, int Defense, int Speed, int Special);
