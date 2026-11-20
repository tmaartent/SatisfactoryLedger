namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>An item a factory takes in or makes: now, at full speed, and how much is short.</summary>
internal sealed record ItemFlowAnswer(string Item, double NowPerMinute, double AtFullSpeedPerMinute, double ShortPerMinute);
