using SatisfactoryLedger.World.Contracts.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A run of the optimal arrangement with what it placed: empty lists until its problem is made.</summary>
/// <param name="Belts">The belts between the arranged factories, routed on the floor below them; none in runs from
/// before it existed.</param>
/// <param name="Unrouted">Per link, the belts that couldn't be routed without crossing.</param>
internal sealed record ArrangementResponse(
    ArrangementSummary Summary,
    IReadOnlyList<ArrangedFactoryResponse> Factories,
    IReadOnlyList<FixedPlaceResponse> Fixed,
    IReadOnlyList<ArrangementLinkDocument> Links,
    IReadOnlyList<RoutedBeltResponse> Belts,
    IReadOnlyList<ArrangementLinkDocument> Unrouted);
