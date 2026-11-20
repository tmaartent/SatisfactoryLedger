namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <param name="Belts">The belts it needs.</param>
/// <param name="WeightPercent">How much it counts in placing; 100 in runs from before weights existed.</param>
/// <param name="Pipes">The pipes it needs; 0 in runs from before pipes were told apart, whose belts counted them.</param>
/// <param name="Loads">What each of its belts carries, then each of its pipes, in the order the router numbers them; none in
/// runs from before loads.</param>
public sealed record ArrangementLinkDocument(
    string From, string To, int Belts, int WeightPercent = 100, int Pipes = 0, IReadOnlyList<BeltLoadDocument>? Loads = null);
