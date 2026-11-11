namespace SatisfactoryLedger.World.Analysis;

/// <summary>What kind of end a factory's output reaches (BR-A18).</summary>
internal enum OutputDestinationKind
{
    /// <summary>A machine or generator of a factory: another one, or this one when its line loops back.</summary>
    Factory,

    /// <summary>A machine or generator no factory has.</summary>
    NoFactory,

    /// <summary>An AWESOME Sink.</summary>
    Sink,

    /// <summary>A consumer that uses up what it gets, by building: a portal, an alien power augmenter.</summary>
    Consumer,

    /// <summary>A storage container, fluid buffer, station or dimensional depot uploader.</summary>
    Storage,

    /// <summary>A belt, lift or pipe that goes nowhere.</summary>
    Nowhere,
}
