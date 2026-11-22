namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// An explanation: its text, the model that wrote it (none when the trace's own note answers), the facts it stands on,
/// and the numbers in it that the facts don't hold, as written.
/// </summary>
internal sealed record ExplanationResponse(string Text, string? Model, FactsResponse Facts, IReadOnlyList<string> UnsupportedNumbers);
