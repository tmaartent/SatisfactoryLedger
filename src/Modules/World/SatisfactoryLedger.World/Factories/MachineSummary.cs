namespace SatisfactoryLedger.World.Factories;

/// <param name="Owner">The factory it belongs to, world-wide: of two factories stored with it from before BR-W5, the oldest.</param>
internal sealed record MachineSummary(string Id, string ClassName, string? Recipe, Guid Owner);
