using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>
/// Turns the world into a flow network: which kind of node each buildable is, what it carries, and its rates as
/// doubles from here on (ADR-0004, amended).
/// </summary>
public static class WorldFlow
{
    /// <param name="fluids">The items that are fluids (Catalog): they go over pipes, every other item over belts.</param>
    public static FlowNetwork From(WorldGraph world, IEnumerable<ItemId> fluids)
    {
        ArgumentNullException.ThrowIfNull(world);

        return new FlowNetwork(world.Buildables.Select(buildable => NodeOf(buildable, world)), world.Connections, fluids);
    }

    private static FlowNode NodeOf(Buildable buildable, WorldGraph world) => buildable switch
    {
        Machine machine => new FlowMachine(machine.Id, PerMinute(machine.Demand), PerMinute(machine.Production)),
        Provider { Offer: { } offer } provider => new FlowProvider(provider.Id, offer.Item, offer.PerMinute.ToDouble()),
        // BR-A9: its rate isn't known (a station, an unknown node), so it supplies what is asked. A station also
        // takes everything loaded onto it.
        Provider provider => new FlowUnknownSource(provider.Id, Count(provider.Stock), Carries.Anything, provider.Burns),
        Consumer consumer => new FlowSink(consumer.Id, consumer.Burns),
        Logistics { Kind: LogisticsKind.Splitter or LogisticsKind.PipeJunction } splitter => new FlowSplitter(splitter.Id, CarriesOf(splitter)),
        Logistics { Kind: LogisticsKind.Merger } merger => new FlowMerger(merger.Id, CarriesOf(merger)),
        Logistics { Kind: LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer } container => Container(container, world),
        Logistics conduit => new FlowConduit(conduit.Id, conduit.Capacity?.ToDouble() ?? double.PositiveInfinity, CarriesOf(conduit)),
        _ => throw new ArgumentException($"{buildable} is not a kind of buildable the flow analysis knows", nameof(buildable)),
    };

    /// <summary>
    /// BR-A7: at the end of a line a container takes everything; inside a line it passes items through.
    /// BR-A9: a container nothing feeds supplies what is asked.
    /// </summary>
    private static FlowNode Container(Logistics container, WorldGraph world)
    {
        if (!world.Downstream(container.Id).Any())
        {
            return new FlowSink(container.Id);
        }

        return world.Upstream(container.Id).Any()
            ? new FlowConduit(container.Id, double.PositiveInfinity, CarriesOf(container))
            : new FlowUnknownSource(container.Id, Count(container.Stock), CarriesOf(container));
    }

    private static Carries CarriesOf(Logistics logistics) =>
        logistics.Kind is LogisticsKind.Pipe or LogisticsKind.PipeJunction or LogisticsKind.FluidBuffer ? Carries.Fluids : Carries.Solids;

    private static Dictionary<ItemId, double> Count(IReadOnlyDictionary<ItemId, decimal> stock) =>
        stock.ToDictionary(entry => entry.Key, entry => (double)entry.Value);

    private static Dictionary<ItemId, double> PerMinute(IReadOnlyDictionary<ItemId, Rate> rates) =>
        rates.ToDictionary(rate => rate.Key, rate => rate.Value.ToDouble());
}
