namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>One belt (or pipe) of a supply line, from one end to the next: what it carries.</summary>
/// <param name="Injection">Whether it brings a second source into the line, by a priority merger at its end (BR-S4).</param>
/// <param name="Overflow">Whether it carries a top-up's overflow on, from a smart splitter at its start (BR-S5).</param>
/// <param name="Onto">For a top-up, the leg it merges onto, by its number among the legs: the line's leg into the same
/// factory, its priority merger on the floor before it (decided 2026-11-18); none where it feeds the factory straight.</param>
/// <param name="Off">For an overflow, the top-up leg whose smart splitter it leaves, by its number; none where the overflow
/// comes straight from the factory.</param>
public sealed record SupplyLeg(
    string From, string To, string Item, bool Pipe, double PerMinute, bool Injection, bool Overflow = false, int? Onto = null, int? Off = null);
