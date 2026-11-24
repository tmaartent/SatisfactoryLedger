namespace SatisfactoryLedger.World.Domain.Pipes;

/// <summary>
/// BR-A22: how high fluid reaches in each piece of a pipe network, estimated (asked 2026-11-24). The save doesn't keep
/// it: the game works it out as it runs.
/// <list type="bullet">
/// <item>A fluid output lifts fluid <see cref="OfFluidOutput"/> above where its pipe starts; a pump its head lift above
/// itself.</item>
/// <item>Along pipes fluid reaches as high as where it came from: how far it flowed, or how low, doesn't matter.</item>
/// <item>A pump's head lift doesn't add to what's left of another's: past it, fluid reaches its head lift above it, or
/// as high as before it when that is higher. Fluid doesn't go back through a pump.</item>
/// <item>Where fluid comes from more than one side, the highest reach counts.</item>
/// <item>Gas needs no head lift (asked 2026-11-24): a piece that carries gas has no reach.</item>
/// </list>
/// </summary>
public static class HeadLift
{
    /// <summary>What every machine's and extractor's fluid output lifts, in metres (asked 2026-11-24).</summary>
    public const double OfFluidOutput = 10;

    /// <summary>
    /// Per piece some fluid output feeds, how high fluid reaches there, in metres. Pieces nothing feeds are left out, and
    /// so are pieces that carry gas.
    /// </summary>
    /// <param name="links">Which pieces touch, either way round.</param>
    public static IReadOnlyDictionary<BuildableId, double> Reaches(
        IReadOnlyCollection<PipePiece> pieces, IEnumerable<(BuildableId A, BuildableId B)> links, IEnumerable<FluidOutput> outputs)
    {
        Dictionary<BuildableId, PipePiece> byId = pieces.ToDictionary(piece => piece.Id);
        ILookup<BuildableId, BuildableId> touching = links
            .SelectMany(link => new[] { (From: link.A, To: link.B), (From: link.B, To: link.A) })
            .ToLookup(link => link.From, link => link.To);
        Dictionary<BuildableId, double> reaches = [];

        // A reach only grows, to one of finitely many heights: each piece is passed on again only when it grew.
        Queue<(BuildableId Piece, double Reach, BuildableId? From)> flowing = new(outputs.Select(output =>
            (output.Pipe, output.HeightMetres + OfFluidOutput, (BuildableId?)null)));
        while (flowing.TryDequeue(out (BuildableId Piece, double Reach, BuildableId? From) next))
        {
            if (!byId.TryGetValue(next.Piece, out PipePiece? piece)
                || piece.CarriesGas
                || (piece.Lift is { } back && back.Outlet == next.From)
                || (reaches.TryGetValue(next.Piece, out double reached) && reached >= next.Reach))
            {
                continue;
            }

            reaches[next.Piece] = next.Reach;
            if (piece.Lift is { } lift)
            {
                flowing.Enqueue((lift.Outlet, Math.Max(next.Reach, piece.TopMetres + lift.HeadLiftMetres), piece.Id));
                continue;
            }

            foreach (BuildableId other in touching[next.Piece])
            {
                flowing.Enqueue((other, next.Reach, piece.Id));
            }
        }

        return reaches;
    }
}
