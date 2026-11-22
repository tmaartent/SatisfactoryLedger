namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>An item: its in-game name, class name, whether it's a solid or a fluid, and whether it's mined or extracted.</summary>
internal sealed record ItemAnswer(string Id, string Name, string Form, bool RawResource);
