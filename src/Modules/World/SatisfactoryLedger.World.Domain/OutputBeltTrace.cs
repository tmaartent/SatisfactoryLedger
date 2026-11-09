namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// The old way of finding a factory: upstream from its output belt, through logistics and machines, up to the machines
/// that make one of its input items. Only used to convert factories defined that way into their machines (BR-W2).
/// </summary>
public sealed class OutputBeltTrace
{
    public OutputBeltTrace(BuildableId outputBelt, IEnumerable<ItemId> inputItems)
    {
        ArgumentNullException.ThrowIfNull(outputBelt);

        OutputBelt = outputBelt;
        InputItems = inputItems.ToHashSet();
    }

    public BuildableId OutputBelt { get; }

    public IReadOnlySet<ItemId> InputItems { get; }

    /// <summary>Whether its output belt is in the world, and is a belt.</summary>
    public bool IsTraceableIn(WorldGraph world) => world.Find(OutputBelt) is Logistics { IsBelt: true };

    /// <summary>
    /// The machines upstream of the output belt. The walk stops at a provider, and at a machine that makes one of the
    /// input items, because that machine belongs to another factory.
    /// </summary>
    public IReadOnlySet<BuildableId> Machines(WorldGraph world)
    {
        Buildable output = world.Find(OutputBelt)
            ?? throw new ArgumentException($"The output belt {OutputBelt} is not in the world", nameof(world));
        if (output is not Logistics { IsBelt: true })
        {
            throw new ArgumentException($"{output} is not a belt, so it can't be an output belt", nameof(world));
        }

        HashSet<BuildableId> machines = [];
        HashSet<BuildableId> seen = [OutputBelt];
        Queue<BuildableId> toVisit = new([OutputBelt]);
        while (toVisit.TryDequeue(out BuildableId? current))
        {
            foreach (Buildable upstream in world.Upstream(current))
            {
                bool isNew = upstream switch
                {
                    Machine machine when !machine.ProducesAnyOf(InputItems) => machines.Add(machine.Id) && seen.Add(machine.Id),
                    Logistics => seen.Add(upstream.Id),
                    _ => false,
                };

                if (isNew)
                {
                    toVisit.Enqueue(upstream.Id);
                }
            }
        }

        return machines;
    }
}
