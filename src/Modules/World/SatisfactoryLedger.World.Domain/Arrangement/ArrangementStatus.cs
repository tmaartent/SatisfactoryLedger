namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>How good the arrangement found is.</summary>
public enum ArrangementStatus
{
    /// <summary>The least belt work there is.</summary>
    Optimal,

    /// <summary>The best found in the time given; there may be better.</summary>
    Feasible,

    /// <summary>No arrangement found in the time given: the factories stay where they are.</summary>
    NotFound,
}
