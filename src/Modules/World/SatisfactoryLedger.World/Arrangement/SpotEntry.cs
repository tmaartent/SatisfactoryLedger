using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>Where a spot outside the area's existing belt comes into it, in squares of the floor, and its way outside.</summary>
internal sealed record SpotEntry(GridRectangle Squares, IReadOnlyList<WorldPoint> Way);
