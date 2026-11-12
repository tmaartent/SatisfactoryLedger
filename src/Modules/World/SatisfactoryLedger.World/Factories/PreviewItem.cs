namespace SatisfactoryLedger.World.Factories;

/// <summary>An item a previewed factory hands over (part 2b(2)).</summary>
/// <param name="PerMinute">What crosses now, by the snapshot's stored analysis; null while that isn't ready.</param>
internal sealed record PreviewItem(string Item, double? PerMinute);
