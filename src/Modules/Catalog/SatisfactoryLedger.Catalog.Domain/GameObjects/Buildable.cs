using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// Anything the player places, with the name the game shows for it: a Freight Platform, not a train docking station.
/// Every buildable class of the Docs file, whatever its section, so pages can name what the save holds.
/// </summary>
public class Buildable : ICatalogEntry<Buildable>
{
    private Buildable(BuildableId id, string name)
    {
        Id = id;
        Name = name;
    }

    public static Buildable Create(BuildableId id, string name)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Buildable(id, name);
    }

    public BuildableId Id { get; }

    public string Name { get; private set; }

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Buildable fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different buildable than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        IsInGame = true;
    }

    public void MarkNoLongerInGame() => IsInGame = false;

    public override bool Equals(object? obj) => obj is Buildable other && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}
