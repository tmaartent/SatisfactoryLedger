namespace SatisfactoryLedger.World.Analysis;

/// <summary>What a factory makes of one item: on its output belt now, and if its machines all ran at 100% (BR-A14).</summary>
/// <param name="Now">What flows onto the output belt per minute.</param>
/// <param name="AtFullSpeed">What the machines that feed the output belt make per minute at 100%, at their clock speeds.</param>
internal sealed record FactoryOutput(string Item, double Now, double AtFullSpeed);