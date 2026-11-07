using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain;

public sealed record GeneratorId
{
    private GeneratorId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static GeneratorId Of(string id) => new(id);
}

/// <summary>A fuel a generator can burn, and what burning it takes and leaves.</summary>
/// <param name="EnergyMegajoules">The energy in one item, or in one cubic metre of a fluid fuel.</param>
/// <param name="Supplemental">The fluid it needs besides the fuel (water), or null.</param>
/// <param name="Byproduct">What it leaves per fuel item burned (uranium waste), or null.</param>
public sealed record GeneratorFuel(ItemId Fuel, decimal EnergyMegajoules, ItemId? Supplemental, ItemId? Byproduct, decimal ByproductPerFuel)
{
    public decimal EnergyMegajoules { get; } = EnergyMegajoules > 0 ? EnergyMegajoules : throw new ArgumentOutOfRangeException(nameof(EnergyMegajoules), EnergyMegajoules, "A fuel has energy");
}

/// <summary>
/// A power generator (BR-A8): it burns a fuel at its power production ÷ the fuel's energy, needs its supplemental
/// fluid at power × its supplemental ratio, and leaves the fuel's byproduct per item burned. At 100% clock speed and
/// full power: the grid's demand isn't in the save.
/// </summary>
public class Generator : ICatalogEntry<Generator>
{
    // EF Core stores this field (it needs a mutable List<T>); the domain only hands out a read-only view.
    private List<GeneratorFuel> _fuels = [];

    // EF Core can only pass scalar properties to a constructor; it fills _fuels afterwards.
    private Generator(GeneratorId id, string name, decimal powerMegawatts, decimal supplementalLitresPerMegajoule)
    {
        Id = id;
        Name = name;
        PowerMegawatts = powerMegawatts;
        SupplementalLitresPerMegajoule = supplementalLitresPerMegajoule;
    }

    public static Generator Create(GeneratorId id, string name, decimal powerMegawatts, decimal supplementalLitresPerMegajoule, IReadOnlyList<GeneratorFuel> fuels)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(powerMegawatts);
        ArgumentOutOfRangeException.ThrowIfNegative(supplementalLitresPerMegajoule);

        return new Generator(id, name, powerMegawatts, supplementalLitresPerMegajoule) { _fuels = [.. fuels] };
    }

    public GeneratorId Id { get; }

    public string Name { get; private set; }

    /// <summary>At 100% clock speed.</summary>
    public decimal PowerMegawatts { get; private set; }

    /// <summary>Litres of its supplemental fluid per megajoule it produces (1.6 for a nuclear plant); 0 when it needs none.</summary>
    public decimal SupplementalLitresPerMegajoule { get; private set; }

    public IReadOnlyList<GeneratorFuel> Fuels => _fuels;

    /// <summary>Fuel items, or cubic metres of a fluid fuel, per minute: power (MJ per second) ÷ energy, × 60.</summary>
    public Rate BurnRate(ItemId fuel) => Rate.FromRecipe(PowerMegawatts, FuelOf(fuel).EnergyMegajoules);

    /// <summary>Cubic metres of its supplemental fluid per minute: power × litres per MJ, × 60, ÷ 1000.</summary>
    public Rate SupplementalRate =>
        SupplementalLitresPerMegajoule == 0 ? Rate.Zero : Rate.FromRecipe(PowerMegawatts * SupplementalLitresPerMegajoule, 1000);

    /// <summary>Byproduct per minute: what it leaves per fuel item, times the items it burns.</summary>
    public Rate ByproductRate(ItemId fuel)
    {
        GeneratorFuel burnt = FuelOf(fuel);
        return burnt.Byproduct is null || burnt.ByproductPerFuel == 0 ? Rate.Zero : BurnRate(fuel) * burnt.ByproductPerFuel;
    }

    private GeneratorFuel FuelOf(ItemId fuel) =>
        _fuels.FirstOrDefault(entry => entry.Fuel == fuel) ?? throw new ArgumentException($"{Id.Id} can't burn {fuel.Id}", nameof(fuel));

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Generator fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different generator than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        PowerMegawatts = fromGame.PowerMegawatts;
        SupplementalLitresPerMegajoule = fromGame.SupplementalLitresPerMegajoule;
        _fuels = [.. fromGame._fuels];
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override bool Equals(object? obj) => obj is Generator other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}
