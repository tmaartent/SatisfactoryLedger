using SatisfactoryLedger.World.Domain.Flow.Objects;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests.Tracing;

/// <summary>What the analysis found about a small world, set by hand: how each machine runs, and what flows where.</summary>
internal sealed class TestReadings
{
    private readonly Dictionary<BuildableId, MachineReading> _machines = [];
    private readonly Dictionary<BuildableId, double> _bottlenecks = [];
    private readonly Dictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> _spare = [];
    private readonly Dictionary<BuildableId, IReadOnlyDictionary<ItemId, double>> _unknown = [];
    private readonly Dictionary<BuildableId, Dictionary<ItemId, double>> _into = [];

    public TestReadings Running(string id) => Machine(id, new MachineReading(MachineLimit.None, 1, new Dictionary<ItemId, double>(), new Dictionary<ItemId, double>()));

    public TestReadings Starved(string id, ItemId item, double share) =>
        Machine(id, new MachineReading(MachineLimit.Starved, share, new Dictionary<ItemId, double> { [item] = share }, new Dictionary<ItemId, double>()));

    public TestReadings Blocked(string id, ItemId item, double share) =>
        Machine(id, new MachineReading(MachineLimit.OutputBlocked, share, new Dictionary<ItemId, double>(), new Dictionary<ItemId, double> { [item] = share }));

    public TestReadings WithoutRecipe(string id) =>
        Machine(id, new MachineReading(MachineLimit.NoRecipe, 0, new Dictionary<ItemId, double>(), new Dictionary<ItemId, double>()));

    /// <summary>A belt or pipe at its capacity while more is offered to it (BR-A12).</summary>
    public TestReadings Bottleneck(string id, double offeredMore)
    {
        _bottlenecks[BuildableId.Of(id)] = offeredMore;
        return this;
    }

    /// <summary>A provider that could deliver more than is taken from it.</summary>
    public TestReadings Spare(string id, ItemId item, double perMinute)
    {
        _spare[BuildableId.Of(id)] = new Dictionary<ItemId, double> { [item] = perMinute };
        return this;
    }

    /// <summary>A station whose supply the save doesn't say, and what the analysis has it deliver per minute.</summary>
    public TestReadings Unknown(string id, ItemId item, double perMinute)
    {
        _unknown[BuildableId.Of(id)] = new Dictionary<ItemId, double> { [item] = perMinute };
        return this;
    }

    /// <summary>What flows into a belt or pipe of an item, per minute, beside what else flows into it.</summary>
    public TestReadings Into(string id, ItemId item, double perMinute)
    {
        (_into.TryGetValue(BuildableId.Of(id), out Dictionary<ItemId, double>? flows) ? flows : _into[BuildableId.Of(id)] = [])[item] = perMinute;
        return this;
    }

    /// <summary>The readings of <paramref name="world"/>: splitters, hubs and machines are read through it.</summary>
    public FlowReadings Build(WorldGraph world) => new(
        world, _machines, _bottlenecks, _spare, _unknown, _into.ToDictionary(entry => entry.Key, entry => (IReadOnlyDictionary<ItemId, double>)entry.Value));

    private TestReadings Machine(string id, MachineReading reading)
    {
        _machines[BuildableId.Of(id)] = reading;
        return this;
    }
}
