using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>What one part's simulation found. Connections are numbered as in that part's own network.</summary>
internal sealed record PartResult(
    IReadOnlyList<Dictionary<ItemId, double>> Flows,
    IReadOnlyDictionary<BuildableId, MachineState> Machines,
    List<Bottleneck> Bottlenecks,
    List<UnusedSupply> Unused,
    List<UnknownSupply> Unknown,
    bool Settled,
    int Sweeps);