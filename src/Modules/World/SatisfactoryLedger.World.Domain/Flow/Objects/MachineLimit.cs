namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <summary>Why a machine runs below 100% (BR-A11).</summary>
public enum MachineLimit
{
    /// <summary>It runs at 100%.</summary>
    None,

    /// <summary>It gets less of an input item than it asks.</summary>
    Starved,

    /// <summary>An output item can't (all) leave.</summary>
    OutputBlocked,

    /// <summary>It has no recipe the analysis knows, so it asks and makes nothing.</summary>
    NoRecipe,
}