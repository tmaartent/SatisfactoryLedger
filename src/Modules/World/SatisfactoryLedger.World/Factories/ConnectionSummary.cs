namespace SatisfactoryLedger.World.Factories;

/// <summary>An output or input of a factory in a snapshot (BR-W4).</summary>
/// <param name="Inside">The factory's machine or logistics on its side.</param>
/// <param name="Outside">What lies across: null when its line ends in a storage container of its own.</param>
/// <param name="Items">What leaves or comes in: item class names.</param>
internal sealed record ConnectionSummary(BuildableReference Inside, BuildableReference? Outside, IReadOnlyList<string> Items);
