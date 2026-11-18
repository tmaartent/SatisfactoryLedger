namespace SatisfactoryLedger.World.Arrangement;

/// <param name="Blocks">A station or drone port, which factories keep off; a node doesn't block.</param>
internal sealed record FixedPlaceResponse(string Id, WorldRectangle Space, bool Blocks);
