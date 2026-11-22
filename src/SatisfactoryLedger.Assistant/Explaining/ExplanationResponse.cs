namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// An explanation: its text, the model that wrote it (none when the trace's own note answers), the facts it stands on,
/// and the numbers it wrote outside the facts' names, as written: it's asked for words only.
/// </summary>
internal sealed record ExplanationResponse(string Text, string? Model, FactsResponse Facts, IReadOnlyList<string> UnsupportedNumbers);
