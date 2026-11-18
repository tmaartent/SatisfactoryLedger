namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>What one belt (or pipe) of a link carries: an item, per minute.</summary>
/// <param name="Injection">Whether it brings a second source into a supply line, by a priority merger at its end.</param>
/// <param name="Overflow">Whether it carries a top-up's overflow on, from a smart splitter at its start.</param>
public sealed record BeltLoadDocument(string Item, double PerMinute, bool Pipe, bool Injection = false, bool Overflow = false);
