using System.Text.Json;

namespace SatisfactoryLedger.Assistant.LedgerTools;

/// <summary>One call of a ledger tool: which, what the tool says it does, what it was asked, and its answer.</summary>
internal sealed record ToolCall(string Tool, string Description, IReadOnlyDictionary<string, object?> Arguments, JsonElement Answer);
