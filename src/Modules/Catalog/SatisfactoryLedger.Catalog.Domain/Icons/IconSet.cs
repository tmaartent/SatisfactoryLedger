namespace SatisfactoryLedger.Catalog.Domain.Icons;

/// <summary>
/// The game's icons (asked 2026-11-23), game data like the rest of the Catalog, from the player's own install through
/// the map tool (ADR-0015, ADR-0032). One per descriptor: items, resources and the descriptors buildings are built from.
/// There is one icon set at a time: an import replaces it whole.
/// </summary>
public sealed class IconSet
{
    private const string BuildingPrefix = "Build_";
    private const string DescriptorPrefix = "Desc_";

    // EF Core binds only scalar constructor parameters; it sets the icons afterwards.
    private IconSet(string gameVersion)
    {
        GameVersion = gameVersion;
    }

    public static IconSet Create(string gameVersion, IReadOnlyList<Icon> icons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        ArgumentNullException.ThrowIfNull(icons);
        if (icons.Any(icon => string.IsNullOrWhiteSpace(icon.ClassName) || icon.Image.Length == 0))
        {
            throw new ArgumentException("Every icon needs a descriptor and an image.", nameof(icons));
        }

        if (icons.Select(icon => icon.ClassName).Distinct(StringComparer.Ordinal).Count() != icons.Count)
        {
            throw new ArgumentException("An icon set holds each descriptor's icon once.", nameof(icons));
        }

        return new IconSet(gameVersion.Trim()) { Icons = icons };
    }

    public string GameVersion { get; }

    public IReadOnlyList<Icon> Icons { get; private init; } = [];

    /// <summary>
    /// The descriptor whose icon a class shows. An item or resource is its own descriptor. A building has none of its
    /// own: the game shows the one it's built from, which it names alike (Build_ConstructorMk1_C, Desc_ConstructorMk1_C).
    /// </summary>
    public static string DescriptorOf(string className)
    {
        ArgumentNullException.ThrowIfNull(className);
        return className.StartsWith(BuildingPrefix, StringComparison.Ordinal)
            ? DescriptorPrefix + className[BuildingPrefix.Length..]
            : className;
    }
}
