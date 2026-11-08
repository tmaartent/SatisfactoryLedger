using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Browsing;

internal sealed record ItemResponse(string Id, string Name, ItemForm Form, bool IsRawResource, bool InGame);