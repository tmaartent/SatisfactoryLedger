using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

/// <param name="FluidColour">A fluid's colour in the game, as "#rrggbb"; null for a solid.</param>
internal sealed record ItemResponse(string Id, string Name, ItemForm Form, bool IsRawResource, bool InGame, string? FluidColour);