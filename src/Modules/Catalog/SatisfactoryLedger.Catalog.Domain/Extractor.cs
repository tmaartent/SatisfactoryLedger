using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>
/// A building that takes a raw resource out of the world: a miner, or an oil, water or resource well extractor.
/// It extracts an amount per cycle, like a recipe makes an amount per cycle.
/// </summary>
public class Extractor : ICatalogEntry<Extractor>
{
    private Extractor(ExtractorId id, string name, ItemForm form, decimal amountPerCycle, decimal cycleSeconds)
    {
        Id = id;
        Name = name;
        Form = form;
        AmountPerCycle = amountPerCycle;
        CycleSeconds = cycleSeconds;
    }

    /// <param name="amountPerCycle">Items, or cubic metres for a fluid, on a normal-purity node at 100% clock speed.</param>
    public static Extractor Create(ExtractorId id, string name, ItemForm form, decimal amountPerCycle, decimal cycleSeconds)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountPerCycle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleSeconds);

        return new Extractor(id, name, form, amountPerCycle, cycleSeconds);
    }

    public ExtractorId Id { get; }

    public string Name { get; private set; }

    /// <summary>Whether it extracts a solid (miners) or a fluid (extractors).</summary>
    public ItemForm Form { get; private set; }

    public decimal AmountPerCycle { get; private set; }

    public decimal CycleSeconds { get; private set; }

    /// <summary>What it extracts per minute on a normal-purity node at 100% clock speed.</summary>
    public Rate ExtractionRate => Rate.FromRecipe(AmountPerCycle, CycleSeconds);

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Extractor fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different extractor than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        Form = fromGame.Form;
        AmountPerCycle = fromGame.AmountPerCycle;
        CycleSeconds = fromGame.CycleSeconds;
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override bool Equals(object? obj) => obj is Extractor other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}
