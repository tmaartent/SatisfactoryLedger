namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>Why a machine runs below 100% (BR-A11): as the domain's <c>MachineLimit</c>, written by name.</summary>
public enum MachineLimit
{
    None,
    Starved,
    OutputBlocked,
    NoRecipe,
}