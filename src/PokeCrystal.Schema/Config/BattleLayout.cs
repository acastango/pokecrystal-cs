namespace PokeCrystal.Schema;

public record BattleLayout(
    string Id,
    BattleSlot[] PlayerSlots,
    BattleSlot[] EnemySlots,
    BattleHudPosition PlayerHud,
    BattleHudPosition EnemyHud,
    BattleHudPosition TextBox,
    BattleHudPosition ActionMenu,
    BattleHudPosition MoveMenu,
    string BackgroundType
) : IIdentifiable;

public record BattleSlot(
    int X,
    int Y,
    float Scale,
    bool FaceLeft,
    int EntryOffsetX,
    int EntryOffsetY,
    float EntryDurationSec
);

public record BattleHudPosition(int X, int Y, int Width, HudAlignment Alignment);

public enum HudAlignment { Left, Center, Right }
