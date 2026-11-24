using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// A pipeline pump: past it, fluid rises up to <see cref="HeadLiftMetres"/> above it (asked 2026-11-24). Its head lift
/// doesn't add to what's left of another's.
/// </summary>
public class Pump : ICatalogEntry<Pump>
{
    private Pump(PumpId id, string name, decimal headLiftMetres)
    {
        Id = id;
        Name = name;
        HeadLiftMetres = headLiftMetres;
    }

    public static Pump Create(PumpId id, string name, decimal headLiftMetres)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(headLiftMetres);

        return new Pump(id, name, headLiftMetres);
    }

    public PumpId Id { get; }

    public string Name { get; private set; }

    /// <summary>How high above itself it lifts fluid, in metres: the Docs file's <c>mDesignPressure</c>.</summary>
    public decimal HeadLiftMetres { get; private set; }

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Pump fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different pump than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        HeadLiftMetres = fromGame.HeadLiftMetres;
        IsInGame = true;
    }

    public void MarkNoLongerInGame() => IsInGame = false;

    public override bool Equals(object? obj) => obj is Pump other && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}
