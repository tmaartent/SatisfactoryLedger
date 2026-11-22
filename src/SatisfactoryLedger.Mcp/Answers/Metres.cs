using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>Where something stands, in metres as the game's map shows them, not the API's centimetres.</summary>
internal sealed record Metres(double X, double Y, double Z)
{
    public static Metres? Of(PositionDocument? position) =>
        position is null ? null : new(Math.Round(position.X / 100, 1), Math.Round(position.Y / 100, 1), Math.Round(position.Z / 100, 1));
}
