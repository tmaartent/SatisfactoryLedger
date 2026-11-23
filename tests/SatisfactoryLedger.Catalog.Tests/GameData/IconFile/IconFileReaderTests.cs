using System.Text;

using SatisfactoryLedger.Catalog.Domain.Icons;
using SatisfactoryLedger.Catalog.GameData.IconFile;

namespace SatisfactoryLedger.Catalog.Tests.GameData.IconFile;

/// <summary>
/// Reading the icons the map tool writes (ADR-0032). No fixture from the game: the icons are the game's own art, never
/// committed; these files are made up, an image of three bytes.
/// </summary>
public class IconFileReaderTests
{
    private static readonly string Image = Convert.ToBase64String([1, 2, 3]);

    private static Task<IconSet> ReadAsync(string json) =>
        IconFileReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), TestContext.Current.CancellationToken);

    private static string File(string icons, int version = 1) =>
        $$"""{ "version": {{version}}, "gameVersion": "1.2", "icons": [ {{icons}} ] }""";

    private static string Icon(string className, string contentType = "image/png") =>
        $$"""{ "className": "{{className}}", "contentType": "{{contentType}}", "image": "{{Image}}" }""";

    [Fact]
    public async Task Each_descriptor_keeps_its_icon_and_what_kind_of_image_it_is()
    {
        IconSet icons = await ReadAsync(File($"{Icon("Desc_IronPlate_C")}, {Icon("Desc_ConstructorMk1_C")}"));

        Assert.Equal("1.2", icons.GameVersion);
        Assert.Equal(["Desc_IronPlate_C", "Desc_ConstructorMk1_C"], icons.Icons.Select(icon => icon.ClassName));
        Assert.Equal([1, 2, 3], icons.Icons[0].Image);
        Assert.Equal("image/png", icons.Icons[0].ContentType);
    }

    [Fact]
    public async Task A_file_of_another_format_version_is_refused()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(File(Icon("Desc_IronPlate_C"), version: 2)));
    }

    [Fact]
    public async Task An_icon_that_is_not_a_png_image_is_refused_as_it_would_be_served_as_what_it_says()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(File(Icon("Desc_IronPlate_C", "text/html"))));
    }
}
