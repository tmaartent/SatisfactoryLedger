namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>How good a computed arrangement is.</summary>
public enum ArrangementQuality
{
    /// <summary>The least belt work there is.</summary>
    Optimal,

    /// <summary>The best found in the time given.</summary>
    Feasible,

    /// <summary>None found in the time given: the factories stay where they are.</summary>
    NotFound,
}
