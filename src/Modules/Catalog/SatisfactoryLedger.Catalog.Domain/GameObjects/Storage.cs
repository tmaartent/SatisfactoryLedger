using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// What a building with an inventory holds (BR-A7): <see cref="Slots"/> of items, each a stack of an item's stack size,
/// or <see cref="FluidCubicMetres"/> of fluid. A storage container, fluid buffer, freight platform, drone port, truck
/// station or dimensional depot uploader. It takes everything until it's full.
/// </summary>
public class Storage : ICatalogEntry<Storage>
{
    private Storage(StorageId id, string name, int slots, decimal fluidCubicMetres)
    {
        Id = id;
        Name = name;
        Slots = slots;
        FluidCubicMetres = fluidCubicMetres;
    }

    public static Storage Create(StorageId id, string name, int slots, decimal fluidCubicMetres)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(slots);
        ArgumentOutOfRangeException.ThrowIfNegative(fluidCubicMetres);
        if (slots == 0 && fluidCubicMetres == 0)
        {
            throw new ArgumentException($"{id.Id} holds nothing: it needs slots or a fluid volume", nameof(slots));
        }

        return new Storage(id, name, slots, fluidCubicMetres);
    }

    public StorageId Id { get; }

    public string Name { get; private set; }

    /// <summary>Its inventory slots; 0 for a fluid buffer.</summary>
    public int Slots { get; private set; }

    /// <summary>The fluid it holds, in m³; 0 for an inventory of slots.</summary>
    public decimal FluidCubicMetres { get; private set; }

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Storage fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different storage than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        Slots = fromGame.Slots;
        FluidCubicMetres = fromGame.FluidCubicMetres;
        IsInGame = true;
    }

    public void MarkNoLongerInGame() => IsInGame = false;

    public override bool Equals(object? obj) => obj is Storage other && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}
