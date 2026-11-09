namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <summary>A source whose rate isn't known, such as a station or a container nothing feeds (BR-A9).</summary>
/// <param name="Stock">What it holds right now, per item. Decides between assumed and confirmed supplied.</param>
/// <param name="Burns">
/// The fuel it burns, for a drone port or truck station: fuel coming in takes only what the line's other consumers
/// leave. Everything else coming in, it takes like a consumer.
/// </param>
/// <param name="BurnRate">
/// For a drone port, how much of its fuel it burns per minute when that's known (BR-A16): it then takes its fuel like
/// any consumer, up to that rate.
/// </param>
public sealed record FlowUnknownSource(
    BuildableId Id,
    IReadOnlyDictionary<ItemId, double> Stock,
    Carries Carries = Carries.Anything,
    IReadOnlySet<ItemId>? Burns = null,
    double? BurnRate = null) : FlowNode(Id);