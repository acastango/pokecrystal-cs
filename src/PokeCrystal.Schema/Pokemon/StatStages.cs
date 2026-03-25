namespace PokeCrystal.Schema;

/// <summary>
/// Stat stage modifiers per combatant. 7 = neutral, 1 = min (-6), 13 = max (+6).
/// This 7-based encoding matches the ASM stat-stage tables directly.
/// </summary>
public record StatStages(
    int Attack,
    int Defense,
    int Speed,
    int SpAtk,
    int SpDef,
    int Accuracy,
    int Evasion
)
{
    public const int Min     = 1;
    public const int Neutral = 7;
    public const int Max     = 13;

    public static StatStages Default => new(7, 7, 7, 7, 7, 7, 7);
}
