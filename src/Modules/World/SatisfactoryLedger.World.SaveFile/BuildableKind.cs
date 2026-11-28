namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// What a buildable does in the analysis, decided from its class once, here (decided 2026-11-28): every reader past
/// the save-file adapter asks the kind, never the class name.
/// </summary>
internal enum BuildableKind
{
    /// <summary>None of the kinds below: a machine, an extractor, a splitter, a container, and the like.</summary>
    Other,

    /// <summary>An AWESOME Sink: what reaches it is sunk.</summary>
    Sink,

    /// <summary>Takes items to run and gives nothing back to the network; shown by building in a factory's handovers.</summary>
    EndConsumer,

    /// <summary>A dimensional depot uploader: takes only what's left over (BR-A8).</summary>
    Uploader,

    /// <summary>Unloads or loads, as its ports say (BR-W1, BR-W8); it stays where it is in an arrangement.</summary>
    Station,

    /// <summary>Burns a fuel to make power (BR-A8). Geothermal burns nothing, so it isn't one.</summary>
    Generator,

    /// <summary>A belt or a lift: as a transport tier, it carries solids.</summary>
    BeltOrLift,

    /// <summary>A pipe piece: as a transport tier, it carries fluids.</summary>
    Pipe,
}
