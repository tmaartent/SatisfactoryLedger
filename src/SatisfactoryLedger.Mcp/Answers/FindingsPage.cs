namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>The findings of one kind: how many there are, and the first of them.</summary>
internal sealed record FindingsPage(string Kind, int Total, IReadOnlyList<object> Findings);
