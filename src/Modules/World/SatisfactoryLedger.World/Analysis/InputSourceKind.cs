namespace SatisfactoryLedger.World.Analysis;

/// <summary>What kind of start what arrives over a factory's input comes from (decided 2026-11-11).</summary>
internal enum InputSourceKind
{
    /// <summary>A machine or generator of a factory: another one, or this one when its own output comes back.</summary>
    Factory,

    /// <summary>A machine or generator no factory has.</summary>
    NoFactory,

    /// <summary>A provider, station or container nothing feeds, each on its own: a Miner, a Water Extractor, a Freight Platform.</summary>
    Building,

    /// <summary>A belt, lift or pipe that starts at nothing.</summary>
    Nowhere,
}
