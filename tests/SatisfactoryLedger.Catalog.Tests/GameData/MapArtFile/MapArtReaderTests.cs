using System.Text;

using SatisfactoryLedger.Catalog.Domain.Map;
using SatisfactoryLedger.Catalog.GameData.MapArtFile;

namespace SatisfactoryLedger.Catalog.Tests.GameData.MapArtFile;

/// <summary>
/// Reading the map art the map tool writes (ADR-0020). No fixture from the game: the map art is the game's own picture,
/// never committed; these files are made up, a tile of three bytes.
/// </summary>
public class MapArtReaderTests
{
    private static readonly string Image = Convert.ToBase64String([1, 2, 3]);

    private static Task<MapArt> ReadAsync(string json) =>
        MapArtReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), TestContext.Current.CancellationToken);

    private static string File(string tiles, int version = 1, string gameVersion = "1.2") =>
        $$"""{ "version": {{version}}, "gameVersion": "{{gameVersion}}", "x0": -324698.168, "y0": -375000, "width": 750000, "height": 750000, "tiles": [ {{tiles}} ] }""";

    private static string Tile(int column, int row) =>
        $$"""{ "column": {{column}}, "row": {{row}}, "contentType": "image/jpeg", "image": "{{Image}}" }""";

    [Fact]
    public async Task The_map_art_covers_the_rectangle_the_file_gives_with_its_tiles()
    {
        MapArt art = await ReadAsync(File($"{Tile(0, 0)}, {Tile(1, 0)}"));

        Assert.Equal("1.2", art.GameVersion);
        Assert.Equal(new MapArtBounds(-324698.168, -375000, 425301.832, 375000), art.Bounds);
        Assert.Equal([(0, 0), (1, 0)], art.Tiles.Select(tile => (tile.Column, tile.Row)));
    }

    [Fact]
    public async Task A_tile_keeps_its_image_and_what_kind_of_image_it_is()
    {
        MapArtTile tile = Assert.Single((await ReadAsync(File(Tile(0, 0)))).Tiles);

        Assert.Equal([1, 2, 3], tile.Image);
        Assert.Equal("image/jpeg", tile.ContentType);
    }

    [Fact]
    public async Task A_file_of_another_format_version_is_refused()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(File(Tile(0, 0), version: 2)));
    }

    [Fact]
    public async Task A_tile_that_is_not_a_jpeg_or_png_is_refused()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadAsync(File($$"""{ "column": 0, "row": 0, "contentType": "text/html", "image": "{{Image}}" }""")));
    }

    [Fact]
    public async Task Tiles_that_do_not_form_a_full_grid_are_refused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ReadAsync(File($"{Tile(0, 0)}, {Tile(1, 1)}")));
    }
}
