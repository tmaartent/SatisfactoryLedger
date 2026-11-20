namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>Everything that brings one item to the arranged factories, and everything that takes it, to plan its supply lines.</summary>
/// <param name="Pipe">Whether it's a fluid, on pipes.</param>
/// <param name="Capacity">What one line carries at most, per minute.</param>
public sealed record ItemSupplyDocument(
    string Item, bool Pipe, double Capacity, IReadOnlyList<SupplyEndDocument> Sources, IReadOnlyList<SupplyEndDocument> Consumers);
