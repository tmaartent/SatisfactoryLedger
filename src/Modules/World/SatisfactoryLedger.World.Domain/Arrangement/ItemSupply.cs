namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>Everything that brings one item to the arranged factories, and everything that takes it, to plan its supply lines.</summary>
/// <param name="Pipe">Whether it's a fluid, on pipes: no injections then (BR-S11).</param>
/// <param name="Capacity">What one line carries at most: the top belt's or pipe's capacity (BR-S1).</param>
/// <param name="Sources">The factories that make it and the spots that bring it, by what they bring now.</param>
/// <param name="Consumers">The factories and spots that take it, by what they take now.</param>
public sealed record ItemSupply(string Item, bool Pipe, double Capacity, IReadOnlyList<SupplyEnd> Sources, IReadOnlyList<SupplyEnd> Consumers);
