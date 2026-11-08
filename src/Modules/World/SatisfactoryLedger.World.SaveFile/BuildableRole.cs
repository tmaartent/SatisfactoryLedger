namespace SatisfactoryLedger.World.SaveFile;

internal enum BuildableRole
{
    /// <summary>Not a class the adapter knows. Ignored, unless it's connected to belts or pipes: then it's reported.</summary>
    Unknown,
    Machine,
    Provider,
    Consumer,
    Logistics,
    /// <summary>A train platform, drone port or truck station: a provider or a consumer, as its linked ports say.</summary>
    Station,
    /// <summary>Known, in the item network, but not modelled yet. Reported, and its connections are left out.</summary>
    Unsupported,
}