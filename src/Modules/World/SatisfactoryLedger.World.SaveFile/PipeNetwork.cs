using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// A connected pipe network (BR-A6): one hub that takes from every output feeding it and shares over every input it
/// feeds, as the game treats a pipe network as one. <see cref="Pipes"/> are its pipes, junctions, pumps, valves and
/// fluid buffers.
/// </summary>
internal sealed record PipeNetwork(BuildableId Hub, IReadOnlyList<BuildableId> Pipes)
{
    /// <summary>The hub's id: named after the network's pipe whose id sorts first, so it stays the same from save to save.</summary>
    public static BuildableId HubOf(BuildableId firstPipe) => BuildableId.Of($"PipeNetwork:{firstPipe.Id}");
}