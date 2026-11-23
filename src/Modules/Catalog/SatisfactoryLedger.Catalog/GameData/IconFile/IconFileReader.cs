using System.Text.Json;

using SatisfactoryLedger.Catalog.Domain.Icons;

namespace SatisfactoryLedger.Catalog.GameData.IconFile;

/// <summary>
/// Reads the icons the map tool writes (ADR-0032). Our own format, version 1; anything else is refused with an
/// <see cref="InvalidDataException"/>, never half read. Icons are served back as the type they say they are, so only
/// PNG gets in: the tool writes nothing else.
/// </summary>
internal static class IconFileReader
{
    private const int SupportedVersion = 1;

    private const string Png = "image/png";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<IconSet> ReadAsync(Stream file, CancellationToken cancellationToken)
    {
        FileDto dto = await JsonSerializer.DeserializeAsync<FileDto>(file, Json, cancellationToken)
            ?? throw new InvalidDataException("The icon file is empty");

        if (dto.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Icon file format version {dto.Version} is not supported, only {SupportedVersion}");
        }

        if (string.IsNullOrWhiteSpace(dto.GameVersion))
        {
            throw new InvalidDataException("The icon file has no game version");
        }

        return IconSet.Create(dto.GameVersion, [.. (dto.Icons ?? []).Select(ToIcon)]);
    }

    private static Icon ToIcon(IconDto icon)
    {
        if (icon.ContentType != Png)
        {
            throw new InvalidDataException($"The icon of {icon.ClassName} is {icon.ContentType ?? "of no type"}, not a PNG image");
        }

        return new Icon(icon.ClassName ?? "", icon.Image ?? [], icon.ContentType);
    }

    private sealed record FileDto(int Version, string? GameVersion, List<IconDto>? Icons);

    private sealed record IconDto(string? ClassName, string? ContentType, byte[]? Image);
}
