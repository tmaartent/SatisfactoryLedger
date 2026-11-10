using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>Reading a solved linear programme by buildable names: only tests ask this way.</summary>
internal static class LinearFlowQueries
{
    extension(LinearFlow flow)
    {
        /// <summary>What flows over the connection from one buildable to another, of one item.</summary>
        public double Flow(BuildableId from, BuildableId to, ItemId item) =>
            Enumerable.Range(0, flow.Network.Edges.Count)
                .Where(edge => flow.Network.Nodes[flow.Network.Edges[edge].From].Id == from && flow.Network.Nodes[flow.Network.Edges[edge].To].Id == to)
                .Sum(edge => flow.Flows[edge].GetValueOrDefault(item));

        /// <summary>What flows into a buildable over all its inputs, of one item.</summary>
        public double Into(BuildableId to, ItemId item) =>
            Enumerable.Range(0, flow.Network.Edges.Count)
                .Where(edge => flow.Network.Nodes[flow.Network.Edges[edge].To].Id == to)
                .Sum(edge => flow.Flows[edge].GetValueOrDefault(item));
    }
}
