using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Pipes;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Map;

/// <summary>
/// BR-A22: per pipe, how much head lift it has to spare, estimated from where things stand. Heights in the snapshot are
/// centimetres, the estimate's metres.
/// </summary>
internal static class PipeHeadLifts
{
    private const double CentimetresPerMetre = 100;

    /// <summary>
    /// Per pipe some fluid output feeds, the metres between how high fluid reaches there and its top: negative when it
    /// climbs above the head lift. Pipes nothing feeds are left out, as are all in a snapshot from before version 15.
    /// </summary>
    public static IReadOnlyDictionary<string, double> ToSpare(SnapshotDocument snapshot)
    {
        // Without which way pumps push, every pipe past one would look above the head lift.
        if (snapshot.Pumps is null)
        {
            return new Dictionary<string, double>();
        }

        Dictionary<string, BuildableDocument> byId = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Dictionary<string, decimal> lifts = (snapshot.Pumps ?? []).ToDictionary(pump => pump.Id, pump => pump.HeadLiftMetres, StringComparer.Ordinal);
        HashSet<string> carryingGas = CarryingGas(snapshot);
        List<PipePiece> pieces =
        [
            .. snapshot.Buildables.Where(IsPiece).Where(buildable => buildable.Curve is { Count: > 0 } || buildable.Position is not null)
                .Select(buildable => new PipePiece(
                    BuildableId.Of(buildable.Id),
                    TopOf(buildable),
                    buildable.PumpsInto is { } outlet && lifts.TryGetValue(buildable.ClassName, out decimal lift)
                        ? new PumpLift((double)lift, BuildableId.Of(outlet))
                        : null,
                    carryingGas.Contains(buildable.Id)))
        ];
        HashSet<string> pieceIds = [.. pieces.Select(piece => piece.Id.Id)];

        // A machine, extractor or station that puts a fluid into a pipe: a fluid output, at the pipe's end beside it.
        Func<BuildableDocument, bool> putsOutFluid = PutsOutFluid(snapshot);
        List<FluidOutput> outputs =
        [
            .. snapshot.Connections
                .Where(connection => pieceIds.Contains(connection.To)
                    && byId.TryGetValue(connection.From, out BuildableDocument? from) && putsOutFluid(from))
                .Select(connection => new FluidOutput(BuildableId.Of(connection.To), EndBeside(byId[connection.To], byId[connection.From].Position)))
        ];

        IReadOnlyDictionary<BuildableId, double> reaches = HeadLift.Reaches(
            pieces, (snapshot.PipeLinks ?? []).Select(link => (BuildableId.Of(link.A), BuildableId.Of(link.B))), outputs);
        return pieces.Where(piece => byId[piece.Id.Id].Curve is { Count: > 0 } && reaches.ContainsKey(piece.Id))
            .ToDictionary(piece => piece.Id.Id, piece => Math.Round(piece.Headroom(reaches[piece.Id]), 1), StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether a building puts fluid out (asked 2026-11-24): a machine whose recipe makes a fluid, an extractor of one, a
    /// station. An all-purpose blueprint's refinery on Pure Iron Ingot has a pipe on its fluid output that stays empty.
    /// </summary>
    private static Func<BuildableDocument, bool> PutsOutFluid(SnapshotDocument snapshot)
    {
        HashSet<string> fluids = [.. snapshot.Fluids];
        Dictionary<string, RecipeDocument> recipes = snapshot.Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);
        Dictionary<string, string> nodes = snapshot.ResourceNodes.ToDictionary(node => node.Name, node => node.Resource, StringComparer.Ordinal);
        return building => building.Role switch
        {
            BuildableDocumentRole.Machine => building.Recipe is { } recipe && recipes.TryGetValue(recipe, out RecipeDocument? made)
                && made.Outputs.Any(output => fluids.Contains(output.Item)),
            BuildableDocumentRole.Provider when (building.Resource ?? (building.Node is { } node ? nodes.GetValueOrDefault(node) : null)) is { } resource =>
                fluids.Contains(resource),
            BuildableDocumentRole.Provider => true,
            _ => false,
        };
    }

    /// <summary>
    /// The pieces of the networks that carry gas: a network holds one fluid, so a gas in any of its pieces at save time
    /// says it. A network empty throughout is taken to carry a liquid.
    /// </summary>
    private static HashSet<string> CarryingGas(SnapshotDocument snapshot)
    {
        HashSet<string> gases = [.. snapshot.Gases ?? []];
        HashSet<string> holdingGas = [.. snapshot.Buildables.Where(piece => piece.Contents?.Keys.Any(gases.Contains) == true).Select(piece => piece.Id)];
        return [.. snapshot.PipeNetworks().Where(network => network.Overlaps(holdingGas)).SelectMany(network => network)];
    }

    private static bool IsPiece(BuildableDocument buildable) =>
        buildable.Kind is LogisticsKind.Pipe or LogisticsKind.PipeJunction or LogisticsKind.FluidBuffer;

    /// <summary>A pipe's highest point of its curve; a pump's, junction's or buffer's where it stands.</summary>
    private static double TopOf(BuildableDocument piece) =>
        (piece.Curve is { Count: > 0 } curve ? curve.Max(point => point.Location.Z) : piece.Position!.Z) / CentimetresPerMetre;

    /// <summary>How high a pipe's end is beside a building: of its two ends, the nearer one.</summary>
    private static double EndBeside(BuildableDocument pipe, PositionDocument? building)
    {
        if (pipe.Curve is not { Count: > 0 } curve)
        {
            return pipe.Position!.Z / CentimetresPerMetre;
        }

        PositionDocument first = curve[0].Location;
        PositionDocument last = curve[^1].Location;
        return (building is null || Distance(first, building) <= Distance(last, building) ? first.Z : last.Z) / CentimetresPerMetre;
    }

    private static double Distance(PositionDocument a, PositionDocument b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
}
