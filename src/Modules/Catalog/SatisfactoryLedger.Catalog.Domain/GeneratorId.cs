namespace SatisfactoryLedger.Catalog.Domain;

public sealed record GeneratorId
{
    private GeneratorId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static GeneratorId Of(string id) => new(id);
}