namespace SatisfactoryLedger.World.SaveFile;

/// <summary>Which way items or fluid move through a port.</summary>
internal enum Port
{
    In,
    Out,
    /// <summary>A pipe end: fluid can go either way, so the direction comes from the machines around it.</summary>
    Undirected,
}