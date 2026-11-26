// The type: inside this class, Machine is its method (a method hides a type of the same name).
using MachineBuilding = SatisfactoryLedger.World.Domain.Machine;

namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// What a snapshot's analysis found about its world, as every finding and trace reads it (decided 2026-11-26): how each
/// machine runs, which belts are bottlenecks, what providers could deliver more, which stations' supply is unknown, and
/// what flows where. The analysis stores only what flows into each belt, lift, pipe and container (ADR-0017); the rest
/// is read through the world, the same way for every reader.
/// </summary>
/// <param name="bottlenecks">Per bottleneck (BR-A12), what's offered to it on top of what it carries, per minute.</param>
/// <param name="spare">Per provider with unused supply, what it could deliver more of each item, per minute.</param>
/// <param name="unknownSupply">Per source whose supply the save doesn't say (BR-A9), what the analysis has it deliver per item and minute.</param>
/// <param name="into">Per belt, lift, pipe or container that carries something, what flows into it per item and minute.</param>
public sealed class FlowReadings(
    WorldGraph world,
    IReadOnlyDictionary<BuildableId, MachineReading> machines,
    IReadOnlyDictionary<BuildableId, double> bottlenecks,
    IReadOnlyDictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> spare,
    IReadOnlyDictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> unknownSupply,
    IReadOnlyDictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> into)
{
    private static readonly IReadOnlyDictionary<ItemId, double> Nothing = new Dictionary<ItemId, double>();

    public MachineReading? Machine(BuildableId id) => machines.GetValueOrDefault(id);

    public bool IsBottleneck(BuildableId id) => bottlenecks.ContainsKey(id);

    /// <summary>What's offered to a bottleneck on top of what it carries, per minute; 0 for anything else.</summary>
    public double OfferedMore(BuildableId id) => bottlenecks.GetValueOrDefault(id);

    /// <summary>What a provider could deliver more of an item than is taken from it, per minute.</summary>
    public double Spare(BuildableId id, ItemId item) => spare.GetValueOrDefault(id)?.GetValueOrDefault(item) ?? 0;

    public bool IsUnknownSupply(BuildableId id) => unknownSupply.ContainsKey(id);

    /// <summary>What a source whose supply the save doesn't say delivered of an item in the analysis, per minute.</summary>
    public double Delivered(BuildableId id, ItemId item) => unknownSupply.GetValueOrDefault(id)?.GetValueOrDefault(item) ?? 0;

    /// <summary>What a piece of logistics carries of an item now, per minute (see <see cref="Carrying"/>); 0 for anything else.</summary>
    public double Carries(BuildableId id, ItemId item) => Carrying(id).GetValueOrDefault(item);

    /// <summary>
    /// What a piece of logistics carries now, per item and minute: a belt, lift, pipe or container what the analysis
    /// found flowing into it; a splitter, merger, pipe junction or pipe network, which the analysis doesn't store, what
    /// the logistics feeding it carry, through other such pieces as far as they go. Nothing for anything else.
    /// </summary>
    public IReadOnlyDictionary<ItemId, double> Carrying(BuildableId id) => Carrying(id, []);

    /// <summary>
    /// What a belt, lift or pipe could carry more, per minute: its capacity less all it carries now. A piece whose
    /// capacity isn't known (a splitter, merger, pipe network or container) sets no limit.
    /// </summary>
    public double Room(BuildableId id) => world.Find(id) is Logistics { Capacity: { } capacity }
        ? capacity.ToDouble() - Carrying(id).Values.Sum()
        : double.PositiveInfinity;

    /// <summary>
    /// What a machine or generator takes of an item now, per minute: its demand at the rate it runs, from its lines and
    /// its stock alike. Nothing when the analysis has no state for it.
    /// </summary>
    public double Takes(BuildableId id, ItemId item) =>
        (Machine(id)?.Utilisation ?? 0) * (world.Find(id)?.Demand.GetValueOrDefault(item)?.ToDouble() ?? 0);

    /// <summary>
    /// What reaches a buildable of an item over its lines now, per minute: what the logistics that end at it carry, not
    /// what it takes from its stock (BR-A17). A machine or generator fed from a pipe network or junction that others
    /// share takes what it takes at the rate it runs: the network's whole flow isn't all for it.
    /// </summary>
    public double Reaching(BuildableId id, ItemId item)
    {
        List<Logistics> lines = [.. world.Upstream(id).OfType<Logistics>()];
        bool shared = world.Find(id) is MachineBuilding or Consumer { Generation: not null }
            && lines.Exists(line => line.Kind is LogisticsKind.PipeNetwork or LogisticsKind.PipeJunction && world.Downstream(line.Id).Skip(1).Any());
        return shared ? Takes(id, item) : lines.Sum(line => Carries(line.Id, item));
    }

    /// <summary>
    /// What flows into a buildable of an item now, per minute: into logistics what it carries; into a machine or
    /// generator, whose intake the analysis doesn't store, what it takes at the rate it runs; into anything else, such
    /// as a drone port, a station or a sink, what reaches it over its lines.
    /// </summary>
    public double Into(BuildableId id, ItemId item) => world.Find(id) switch
    {
        Logistics => Carries(id, item),
        MachineBuilding or Consumer { Generation: not null } => Takes(id, item),
        _ => Reaching(id, item),
    };

    /// <summary>
    /// What a buildable sends on of an item, per minute: what a piece of logistics carries; what a machine or generator
    /// makes at the rate it runs, nothing when the analysis has no state for it; what a provider delivers, or for one
    /// whose supply is unknown, what the analysis has it deliver.
    /// </summary>
    public double OutOf(BuildableId id, ItemId item) => world.Find(id) switch
    {
        Logistics => Carries(id, item),
        Provider { Offer: { } offer } when offer.Item == item => offer.PerMinute.ToDouble() - Spare(id, item),
        Provider => Delivered(id, item),
        { } other => (Machine(id)?.Utilisation ?? 0) * (other.Production.GetValueOrDefault(item)?.ToDouble() ?? 0),
        null => 0,
    };

    /// <summary>What a machine or generator would make more of an item at 100%, per minute: what it leaves unmade now.</summary>
    public double MakesMore(BuildableId id, ItemId item) =>
        (1 - (Machine(id)?.Utilisation ?? 0)) * (world.Find(id)?.Production.GetValueOrDefault(item)?.ToDouble() ?? 0);

    private IReadOnlyDictionary<ItemId, double> Carrying(BuildableId id, HashSet<BuildableId> passed) => world.Find(id) switch
    {
        Logistics { Kind: LogisticsKind.Splitter or LogisticsKind.Merger or LogisticsKind.PipeJunction or LogisticsKind.PipeNetwork } when passed.Add(id) =>
            world.Upstream(id).OfType<Logistics>()
                .SelectMany(from => Carrying(from.Id, passed))
                .GroupBy(flow => flow.Key)
                .ToDictionary(flow => flow.Key, flow => flow.Sum(part => part.Value)),
        Logistics => into.GetValueOrDefault(id) ?? Nothing,
        _ => Nothing,
    };
}
