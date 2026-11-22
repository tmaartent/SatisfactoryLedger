namespace SatisfactoryLedger.Assistant.LedgerTools;

/// <summary>A ledger tool couldn't answer: an analysis still running, a recipe the building doesn't have. The message says why.</summary>
internal sealed class LedgerToolException(string message) : Exception(message);
