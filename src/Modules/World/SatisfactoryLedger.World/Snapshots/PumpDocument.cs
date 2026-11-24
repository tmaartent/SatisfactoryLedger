namespace SatisfactoryLedger.World.Snapshots;

/// <summary>A pump's head lift, as the Catalog said at import (BR-A22).</summary>
/// <param name="Id">The building's class name, e.g. <c>Build_PipelinePumpMk2_C</c>.</param>
internal sealed record PumpDocument(string Id, decimal HeadLiftMetres);
