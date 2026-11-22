namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>
/// What a snapshot's analysis found, as a trace reads it: how each machine runs, which belts are bottlenecks, what
/// providers could deliver more, which stations' supply is unknown, and what flows into each belt, lift and pipe.
/// </summary>
/// <param name="bottlenecks">Per bottleneck (BR-A12), what's offered to it on top of what it carries, per minute.</param>
/// <param name="spare">Per provider with unused supply, what it could deliver more of each item, per minute.</param>
/// <param name="unknownSupply">Per source whose supply the save doesn't say (BR-A9), what the analysis has it deliver per item and minute.</param>
/// <param name="into">Per belt, lift, pipe or container that carries something, what flows into it per item and minute.</param>
public sealed class FlowReadings(
    IReadOnlyDictionary<BuildableId, MachineReading> machines,
    IReadOnlyDictionary<BuildableId, double> bottlenecks,
    IReadOnlyDictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> spare,
    IReadOnlyDictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> unknownSupply,
    IReadOnlyDictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> into)
{
    public MachineReading? Machine(BuildableId id) => machines.GetValueOrDefault(id);

    public bool IsBottleneck(BuildableId id) => bottlenecks.ContainsKey(id);

    public double Spare(BuildableId id, ItemId item) => spare.GetValueOrDefault(id)?.GetValueOrDefault(item) ?? 0;

    public bool IsUnknownSupply(BuildableId id) => unknownSupply.ContainsKey(id);

    /// <summary>What a source whose supply the save doesn't say delivered of an item in the analysis, per minute.</summary>
    public double Delivered(BuildableId id, ItemId item) => unknownSupply.GetValueOrDefault(id)?.GetValueOrDefault(item) ?? 0;

    public double Into(BuildableId id, ItemId item) => into.GetValueOrDefault(id)?.GetValueOrDefault(item) ?? 0;

    /// <summary>
    /// What a buildable sends on of an item, per minute: what flows into a piece of logistics; what a machine or
    /// generator makes at the rate it runs; what a provider delivers, or for one whose supply is unknown, what the
    /// analysis has it deliver.
    /// </summary>
    public double OutOf(Buildable buildable, ItemId item)
    {
        double running = Machine(buildable.Id)?.Utilisation ?? 1;
        return buildable switch
        {
            Logistics => Into(buildable.Id, item),
            Machine machine => running * (machine.Production.GetValueOrDefault(item)?.ToDouble() ?? 0),
            Consumer { Generation: { Byproduct: { } waste } generation } when waste == item => running * generation.Leaves.ToDouble(),
            Provider { Offer: { } offer } when offer.Item == item => offer.PerMinute.ToDouble() - Spare(buildable.Id, item),
            Provider => Delivered(buildable.Id, item),
            _ => 0,
        };
    }
}
