namespace SatisfactoryLedger.World.Factories;

/// <param name="Machines">Its machines as stored (BR-W2).</param>
/// <param name="OutputBelt">Only for a factory defined by its output belt that couldn't be converted yet.</param>
/// <param name="InputItems">Only for a factory defined by its output belt that couldn't be converted yet.</param>
internal sealed record FactoryResponse(
    Guid Id,
    string World,
    string Name,
    IReadOnlyList<BuildableReference> Machines,
    string? OutputBelt,
    IReadOnlyList<string>? InputItems)
{
    public static FactoryResponse From(StoredFactory factory) => new(
        factory.Id,
        factory.World,
        factory.Name,
        [.. factory.Machines.Select(machine => new BuildableReference(machine.Id, machine.ClassName))],
        factory.OutputBelt,
        factory.InputItems);
}
