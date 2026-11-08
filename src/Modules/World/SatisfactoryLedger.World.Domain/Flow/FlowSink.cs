namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>Takes everything it's offered: a consumer, or a container at the end of a line (BR-A7, BR-A8).</summary>
/// <param name="Burns">For a station that loads: the fuel it burns, which takes only what the others leave.</param>
/// <param name="LeftoversOnly">
/// For a dimensional depot uploader: it takes everything only from what the line's other consumers leave. In steady
/// state a depot is full (BR-A8).
/// </param>
/// <param name="BurnRate">For a drone port that loads: how much of its fuel it burns per minute, when known (BR-A16).</param>
public sealed record FlowSink(BuildableId Id, IReadOnlySet<ItemId>? Burns = null, bool LeftoversOnly = false, double? BurnRate = null) : FlowNode(Id);