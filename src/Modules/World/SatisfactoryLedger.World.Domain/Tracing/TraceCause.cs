namespace SatisfactoryLedger.World.Domain.Tracing;

/// <summary>Why a shortage or an output blockage starts where it does (decided 2026-11-20).</summary>
public enum TraceCause
{
    /// <summary>Shortage: the producers run at full speed and all they make arrives, but it's not enough.</summary>
    TooFewProducers,

    /// <summary>A belt, lift or pipe on the way is at its capacity while more is offered to it (BR-A12).</summary>
    BeltFull,

    /// <summary>Shortage: a miner or extractor delivers all it can.</summary>
    SourceAtLimit,

    /// <summary>Shortage: the producers make enough for here, but part of it goes to other machines first.</summary>
    TakenByOthers,

    /// <summary>Shortage: no producer is connected; the line starts at nothing.</summary>
    NothingBringsIt,

    /// <summary>A machine on the line has no recipe set.</summary>
    NoRecipe,

    /// <summary>Shortage: a station or container whose supply the save doesn't say.</summary>
    UnknownSupply,

    /// <summary>Shortage: a producer of the item can't get its own output away.</summary>
    ProducerOutputBlocked,

    /// <summary>Blockage: the consumers run at full speed on all that's sent, and take no more.</summary>
    TooFewConsumers,

    /// <summary>Blockage: the consumers run at full speed, partly on what other producers send them.</summary>
    SuppliedByOthers,

    /// <summary>Blockage: the consumers are short of another input, so they can't take more of this one.</summary>
    ConsumerStarved,

    /// <summary>Blockage: the line ends in a full container or station.</summary>
    StorageFull,

    /// <summary>Blockage: the line ends at nothing, or at a machine that doesn't take the item.</summary>
    NowhereToGo,

    /// <summary>Blockage: the line ends at a drone port with the item in its fuel slot where no drone refuels (issue #44).</summary>
    NoDroneRefuels,

    /// <summary>The trace found no reason: providers with supply to spare, and nothing in between that explains it.</summary>
    Unclear,
}
