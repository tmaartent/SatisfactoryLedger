namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What flows through a pipe in the model, per fluid and minute. The model takes a pipe network as one (BR-A6): only a
/// pipe between it and a machine carries a flow of its own. For a pipe inside it, <paramref name="OfNetwork"/> is true and
/// the flow is what its whole network takes in.
/// </summary>
/// <param name="Capacity">What the pipe carries at most per minute (BR-A3); null when the Catalog doesn't know its tier.</param>
internal sealed record PipeFlowFinding(string Id, IReadOnlyDictionary<string, double> PerMinute, bool OfNetwork, double? Capacity);
