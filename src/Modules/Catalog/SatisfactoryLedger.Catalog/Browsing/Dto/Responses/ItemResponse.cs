using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

internal sealed record ItemResponse(string Id, string Name, ItemForm Form, bool IsRawResource, bool InGame);