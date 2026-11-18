using SatisfactoryLedger.World.Contracts.Arrangement;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A run of the optimal arrangement with what it placed: empty lists until its problem is made.</summary>
internal sealed record ArrangementResponse(
    ArrangementSummary Summary,
    IReadOnlyList<ArrangedFactoryResponse> Factories,
    IReadOnlyList<FixedPlaceResponse> Fixed,
    IReadOnlyList<ArrangementLinkDocument> Links);
