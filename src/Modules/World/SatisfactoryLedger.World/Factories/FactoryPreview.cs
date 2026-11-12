namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// What chosen machines would make a factory in a snapshot, before it's saved (part 2b(2)): the logistics it would own
/// (BR-W3) and where it would hand over (BR-W4).
/// </summary>
/// <param name="Logistics">The ids of the belts, lifts, pipes, splitters and containers it would own.</param>
/// <param name="Outputs">Where it would hand over to the world, line by line: the 3D view marks them.</param>
/// <param name="Inputs">Where the world would hand over to it, line by line.</param>
/// <param name="Leaving">Per item, what leaves over its outputs now, most first.</param>
/// <param name="Arriving">Per item, what arrives over its inputs now, most first.</param>
internal sealed record FactoryPreview(
    IReadOnlyList<string> Logistics,
    IReadOnlyList<ConnectionSummary> Outputs,
    IReadOnlyList<ConnectionSummary> Inputs,
    IReadOnlyList<PreviewItem> Leaving,
    IReadOnlyList<PreviewItem> Arriving);
