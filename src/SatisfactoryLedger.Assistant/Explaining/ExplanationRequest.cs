namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// Explain a finding: the machines of one building and recipe in a snapshot, by the class names the web app has, and
/// what holds them back. <see cref="Model"/> is a key of <c>ChatModels</c>; the default when left out.
/// </summary>
internal sealed record ExplanationRequest(string SnapshotId, string Building, string? Recipe, HeldBack Limit, string? Model);
