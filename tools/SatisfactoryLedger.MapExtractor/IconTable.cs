using System.Text.Json;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// The icons the Catalog imports (ADR-0032): each descriptor's, as PNG. Nothing of the game's format gets past this
/// class.
/// </summary>
internal sealed record IconTable(int Version, string GameVersion, IReadOnlyList<IconEntry> Icons)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IconTable Build(string gameVersion, IEnumerable<GameIcon> icons) =>
        new(CurrentVersion, gameVersion, [.. icons.OrderBy(icon => icon.ClassName, StringComparer.Ordinal).Select(icon => new IconEntry(icon.ClassName, "image/png", icon.Png))]);

    /// <summary>Byte arrays are written as base64, as System.Text.Json does. Not indented: it's tens of megabytes.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
