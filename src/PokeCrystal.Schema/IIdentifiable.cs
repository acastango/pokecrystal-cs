namespace PokeCrystal.Schema;

/// <summary>
/// Marker interface for data types that have a string ID used as their
/// registry key. All L0 data records that can be looked up by ID implement this.
/// </summary>
public interface IIdentifiable
{
    string Id { get; }
}
