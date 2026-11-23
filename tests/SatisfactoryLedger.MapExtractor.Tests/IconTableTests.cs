using System.Text.Json;

namespace SatisfactoryLedger.MapExtractor.Tests;

/// <summary>From the game's icons to the file the Catalog imports (ADR-0032).</summary>
public class IconTableTests
{
    [Fact]
    public void Each_descriptor_s_icon_is_written_as_a_png_in_order_of_its_class_name()
    {
        IconTable table = IconTable.Build("1.2", [new GameIcon("Desc_Wire_C", [1]), new GameIcon("Desc_IronPlate_C", [2])]);

        Assert.Equal(
            [new IconEntry("Desc_IronPlate_C", "image/png", [2]), new IconEntry("Desc_Wire_C", "image/png", [1])],
            table.Icons,
            (a, b) => a.ClassName == b.ClassName && a.ContentType == b.ContentType && a.Image.SequenceEqual(b.Image));
    }

    [Fact]
    public void The_file_has_its_format_version_and_the_game_s_and_each_image_as_base64()
    {
        using JsonDocument json = JsonDocument.Parse(IconTable.Build("1.2", [new GameIcon("Desc_Wire_C", [1, 2, 3])]).ToJson());

        JsonElement root = json.RootElement;
        Assert.Equal((1, "1.2"), (root.GetProperty("version").GetInt32(), root.GetProperty("gameVersion").GetString()));
        Assert.Equal("AQID", root.GetProperty("icons")[0].GetProperty("image").GetString());
    }
}
