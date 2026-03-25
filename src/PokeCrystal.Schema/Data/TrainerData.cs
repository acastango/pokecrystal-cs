namespace PokeCrystal.Schema;

/// <summary>
/// Static trainer definition — immutable, loaded from data/trainers/.
/// AI strategy key references IAIStrategy in L2.
/// </summary>
public record TrainerData(
    string Id,
    string ClassId,
    string Name,
    TrainerPartyEntry[] Party,
    string AIStrategyKey,
    string IntroTextRef,
    string WinTextRef,
    string LoseTextRef
) : IIdentifiable;

public record TrainerPartyEntry(
    string SpeciesId,
    byte Level,
    string? HeldItemId,
    string[]? Moves        // null = default learnset for level
);
