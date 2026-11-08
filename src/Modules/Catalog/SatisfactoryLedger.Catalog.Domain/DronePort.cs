namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>
/// The drone port (BR-A16): a round trip of its drone costs <see cref="TripEnergyMegajoules"/>, plus
/// <see cref="TripEnergyPerMetreMegajoules"/> for every metre flown. The drone pays for it with the fuel at its home port.
/// </summary>
public class DronePort : ICatalogEntry<DronePort>
{
    // EF Core stores this field (it needs a mutable List<T>); the domain only hands out a read-only view.
    private List<DroneFuel> _fuels = [];

    // EF Core can only pass scalar properties to a constructor; it fills _fuels afterwards.
    private DronePort(DronePortId id, string name, decimal tripEnergyMegajoules, decimal tripEnergyPerMetreMegajoules)
    {
        Id = id;
        Name = name;
        TripEnergyMegajoules = tripEnergyMegajoules;
        TripEnergyPerMetreMegajoules = tripEnergyPerMetreMegajoules;
    }

    public static DronePort Create(DronePortId id, string name, decimal tripEnergyMegajoules, decimal tripEnergyPerMetreMegajoules, IReadOnlyList<DroneFuel> fuels)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tripEnergyMegajoules);
        ArgumentOutOfRangeException.ThrowIfNegative(tripEnergyPerMetreMegajoules);

        return new DronePort(id, name, tripEnergyMegajoules, tripEnergyPerMetreMegajoules) { _fuels = [.. fuels] };
    }

    public DronePortId Id { get; }

    public string Name { get; private set; }

    /// <summary>What every round trip costs, however short.</summary>
    public decimal TripEnergyMegajoules { get; private set; }

    /// <summary>What every metre flown adds, there and back.</summary>
    public decimal TripEnergyPerMetreMegajoules { get; private set; }

    public IReadOnlyList<DroneFuel> Fuels => _fuels;

    /// <summary>The energy in one item of this fuel; null when a drone can't burn it.</summary>
    public decimal? EnergyOf(ItemId fuel) => _fuels.FirstOrDefault(entry => entry.Fuel == fuel)?.EnergyMegajoules;

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(DronePort fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different drone port than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        TripEnergyMegajoules = fromGame.TripEnergyMegajoules;
        TripEnergyPerMetreMegajoules = fromGame.TripEnergyPerMetreMegajoules;
        _fuels = [.. fromGame._fuels];
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override bool Equals(object? obj) => obj is DronePort other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}