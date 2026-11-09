using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Browsing;

internal sealed record ResourceNodeResponse(
    string Name, ResourceNodeKind Kind, string Resource, Purity Purity, PositionResponse Position, bool InGame);