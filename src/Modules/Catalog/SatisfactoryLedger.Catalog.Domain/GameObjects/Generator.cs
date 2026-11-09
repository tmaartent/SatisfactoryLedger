using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// A power generator (BR-A8), as the Docs file has it: its power, the litres of supplemental fluid (water) it needs per
/// megajoule, and per fuel the energy, supplemental fluid and byproduct. The World module turns these into rates.
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