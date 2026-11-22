using System.Text.Json;

namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>A factory as traced in a snapshot (BR-W3); its machines are only counted here.</summary>
internal sealed record FactoryTrace(string FactoryId, string Name, bool Traceable, IReadOnlyList<JsonElement> Machines);
