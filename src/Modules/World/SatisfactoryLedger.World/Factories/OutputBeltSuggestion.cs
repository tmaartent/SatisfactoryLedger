using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <param name="Items">The items on the belt: what the machines feeding it produce.</param>
/// <param name="Into">What the belt ends in: a storage container or consumer, or nothing.</param>
/// <param name="Machines">
/// Per building class, how many machines a factory with this output belt would contain, before any input items are
/// declared. It helps telling candidates apart.
/// </param>
internal sealed record OutputBeltSuggestion(
    string Belt,
    string ClassName,
    IReadOnlyList<string> Items,
    IReadOnlyList<BuildableReference> Into,
    PositionDocument? Position,
    IReadOnlyDictionary<string, int> Machines);