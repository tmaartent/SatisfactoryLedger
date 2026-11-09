using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Browsing;

internal sealed record TransportTierResponse(string Id, string Name, TransportForm Form, int Capacity, bool InGame);