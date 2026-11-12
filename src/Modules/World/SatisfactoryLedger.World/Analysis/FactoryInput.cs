namespace SatisfactoryLedger.World.Analysis;

/// <summary>What a factory takes in of one item, and where it comes from (decided 2026-11-11).</summary>
/// <param name="Now">What arrives over its inputs per minute.</param>
/// <param name="AtFullSpeed">
/// What it needs from outside per minute at 100%: what all its machines take of it, less what its own machines make of it.
/// </param>
/// <param name="Short">What it would need more at 100% than arrives now.</param>
/// <param name="Sources">Where what arrives comes from, most first.</param>
internal sealed record FactoryInput(string Item, double Now, double AtFullSpeed, double Short, IReadOnlyList<InputSource> Sources);
