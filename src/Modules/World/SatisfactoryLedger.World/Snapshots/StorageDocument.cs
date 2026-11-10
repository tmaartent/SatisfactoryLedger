namespace SatisfactoryLedger.World.Snapshots;

/// <summary>What a building with an inventory holds, as the Catalog said at import (BR-A7).</summary>
/// <param name="Id">The building's class name.</param>
/// <param name="Slots">Its inventory slots; 0 for a fluid buffer.</param>
/// <param name="FluidCubicMetres">The fluid it holds in m³; 0 for an inventory of slots.</param>
internal sealed record StorageDocument(string Id, int Slots, decimal FluidCubicMetres);
