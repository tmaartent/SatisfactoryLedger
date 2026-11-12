namespace SatisfactoryLedger.World.Factories;

/// <param name="Machines">Its machines as stored (BR-W2).</param>
internal sealed record FactoryResponse(Guid Id, string World, string Name, IReadOnlyList<BuildableReference> Machines)
{
    public static FactoryResponse From(StoredFactory factory) => new(
        factory.Id,
        factory.World,
        factory.Name,
        [.. factory.Machines.Select(machine => new BuildableReference(machine.Id, machine.ClassName))]);
}
