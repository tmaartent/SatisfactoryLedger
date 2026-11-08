namespace SatisfactoryLedger.World.Factories;

internal sealed record FactoryResponse(Guid Id, string World, string Name, string OutputBelt, IReadOnlyList<string> InputItems)
{
    public static FactoryResponse From(StoredFactory factory) =>
        new(factory.Id, factory.World, factory.Name, factory.OutputBelt, factory.InputItems);
}