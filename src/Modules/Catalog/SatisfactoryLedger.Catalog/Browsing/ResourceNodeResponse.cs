using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Browsing;

internal sealed record ResourceNodeResponse(
    string Name, ResourceNodeKind Kind, string Resource, Purity Purity, PositionResponse Position, bool InGame);