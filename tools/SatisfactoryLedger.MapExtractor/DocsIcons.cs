using System.Text.Json;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// Which texture is each descriptor's icon, from the game's Docs file (ADR-0032): its big icon, as the game's menus
/// show it. The Docs file names it as an object of the game (<c>Texture2D /Game/…/X.X</c>); this gives its path in the
/// game's files, as CUE4Parse loads it.
/// </summary>
internal static class DocsIcons
{
    private const string BigIcon = "mPersistentBigIcon";
    private const string Texture = "Texture2D ";
    private const string GameRoot = "/Game/";
    private const string ContentRoot = "FactoryGame/Content/";

    public static IReadOnlyDictionary<string, string> Read(string docs)
    {
        using JsonDocument json = JsonDocument.Parse(docs);
        Dictionary<string, string> icons = new(StringComparer.Ordinal);
        foreach (JsonElement section in json.RootElement.EnumerateArray())
        {
            foreach (JsonElement descriptor in section.GetProperty("Classes").EnumerateArray())
            {
                if (descriptor.TryGetProperty(BigIcon, out JsonElement icon)
                    && icon.GetString() is { } path
                    && path.StartsWith(Texture + GameRoot, StringComparison.Ordinal))
                {
                    icons[descriptor.GetProperty("ClassName").GetString()!] = ContentRoot + path[(Texture + GameRoot).Length..];
                }
            }
        }

        return icons;
    }
}
