using System.Text.Json;

namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>What an explanation stands on: the tool called, its arguments, and its answer as the model got it.</summary>
internal sealed record FactsResponse(string Tool, IReadOnlyDictionary<string, object?> Arguments, JsonElement Answer);
