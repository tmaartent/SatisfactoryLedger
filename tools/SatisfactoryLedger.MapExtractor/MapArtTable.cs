using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// The map art the Catalog imports (ADR-0020): the rectangle of the world the game's map picture covers, in
/// centimetres, and its tiles. The game's capture actor stands over the middle and saw a square; its textures are
/// named <c>Map_column-row</c>. Nothing of the game's format gets past this class.
/// </summary>
internal sealed partial record MapArtTable(
    int Version, string GameVersion, double X0, double Y0, double Width, double Height, IReadOnlyList<MapArtTileEntry> Tiles)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static MapArtTable Build(string gameVersion, MapCapture capture)
    {
        List<MapArtTileEntry> tiles =
        [
            .. capture.Images.Select(ToTile).OrderBy(tile => tile.Column).ThenBy(tile => tile.Row)
        ];
        double half = capture.Width / 2;
        return new MapArtTable(CurrentVersion, gameVersion, capture.CentreX - half, capture.CentreY - half, capture.Width, capture.Width, tiles);
    }

    /// <summary>Byte arrays are written as base64, as System.Text.Json does.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);

    private static MapArtTileEntry ToTile(MapImage image)
    {
        Match match = TileName().Match(image.Name);
        if (!match.Success)
        {
            throw new InvalidDataException($"The map texture {image.Name} isn't named Map_column-row");
        }

        return new MapArtTileEntry(
            int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture),
            "image/jpeg",
            image.Jpeg);
    }

    [GeneratedRegex(@"^Map_(?<column>\d+)-(?<row>\d+)$")]
    private static partial Regex TileName();
}
