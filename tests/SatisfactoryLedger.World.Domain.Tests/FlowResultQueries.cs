using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// What the tests ask of a steady state. Built on the data a <see cref="FlowResult"/> carries, so production needs none
/// of it. Linked into the other World test projects too.
/// </summary>
internal static class FlowResultQueries
{
    extension(FlowResult result)
    {
        /// <summary>True when every part settled; false when one stopped at the limit of sweeps.</summary>
        public bool Settled => result.Parts.All(part => part.Settled);

        /// <summary>The most sweeps any part took.</summary>
        public int Sweeps => result.Parts.Count == 0 ? 0 : result.Parts.Max(part => part.Sweeps);

        public MachineState Machine(BuildableId id) => result.Machines.Single(machine => machine.Machine == id);

        /// <summary>The part a buildable belongs to: whether its numbers settled.</summary>
        public FlowPart PartOf(BuildableId id) => result.Parts.Single(part => part.Buildables.Contains(id));

        /// <summary>What flows of an item over the connection from one buildable to another, per minute.</summary>
        public double Flow(BuildableId from, BuildableId to, ItemId item) =>
            result.Connections.Where(connection => connection.From == from && connection.To == to).Sum(connection => connection.Items.GetValueOrDefault(item));
    }
}
