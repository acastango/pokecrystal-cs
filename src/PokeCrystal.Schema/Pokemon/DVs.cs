namespace PokeCrystal.Schema;

/// <summary>
/// Gen 2 Determinant Values — 4 bits per stat, packed as nibbles.
/// HP DV is derived from the low bit of each of the four stat DVs.
/// Shiny condition: all four DVs must equal exactly 10.
/// </summary>
public record DVs(byte Attack, byte Defense, byte Speed, byte Special)
{
    public byte HpDv =>
        (byte)(((Attack & 1) << 3) | ((Defense & 1) << 2) | ((Speed & 1) << 1) | (Special & 1));

    public bool IsShiny => Attack == 10 && Defense == 10 && Speed == 10 && Special == 10;
}
