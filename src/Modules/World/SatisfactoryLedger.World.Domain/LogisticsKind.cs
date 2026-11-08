namespace SatisfactoryLedger.World.Domain;

public enum LogisticsKind
{
    Belt,
    Lift,
    Splitter,
    Merger,
    /// <summary>A storage container: holds solids.</summary>
    StorageContainer,
    /// <summary>A fluid buffer or industrial fluid buffer: holds fluids.</summary>
    FluidBuffer,
    /// <summary>A pipeline segment, pump or valve.</summary>
    Pipe,
    /// <summary>A pipeline junction: splits and merges fluid.</summary>
    PipeJunction,
    /// <summary>
    /// A connected pipe network as one (BR-A6): it takes from every output that feeds it and shares over every input it
    /// feeds. Not a buildable of the game: the save reader makes it, as the game treats a pipe network as one.
    /// </summary>
    PipeNetwork,
}