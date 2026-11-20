using System.Text.Json;

namespace SatisfactoryLedger.MapExtractor.Tests;

/// <summary>From what the game's map capture says to the map art the Catalog imports (ADR-0020).</summary>
public class MapArtTableTests
{
    // As in the game: the capture actor at (50,301.832, 0), looking down on a square 750,000 cm wide.
    private static MapCapture Capture(params string[] names) =>
        new(50301.832, 0, 750000, [.. (names.Length > 0 ? names : ["Map_0-0", "Map_1-0", "Map_0-1", "Map_1-1"]).Select(name => new MapImage(name, [1, 2, 3]))]);

    [Fact]
    public void The_map_art_covers_the_square_the_game_captured_around_its_capture_actor()
    {
        MapArtTable table = MapArtTable.Build("1.2", Capture());

        Assert.Equal((50301.832 - 375000, -375000.0, 750000.0, 750000.0), (table.X0, table.Y0, table.Width, table.Height));
    }

    [Fact]
    public void A_tile_takes_its_column_and_row_from_its_name()
    {
        MapArtTable table = MapArtTable.Build("1.2", Capture());

        Assert.Equal([(0, 0), (0, 1), (1, 0), (1, 1)], table.Tiles.Select(tile => (tile.Column, tile.Row)));
    }

    [Fact]
    public void A_tile_with_a_name_that_is_not_a_column_and_row_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => MapArtTable.Build("1.2", Capture("Map_Test_Gradient")));
    }

    [Fact]
    public void The_tiles_are_jpeg_images_written_as_base64()
    {
        string json = MapArtTable.Build("1.2", Capture("Map_0-0")).ToJson();

        JsonElement tile = JsonDocument.Parse(json).RootElement.GetProperty("tiles")[0];
        Assert.Equal("image/jpeg", tile.GetProperty("contentType").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), tile.GetProperty("image").GetString());
    }

    [Fact]
    public void The_map_art_carries_its_game_version_and_format_version()
    {
        JsonElement root = JsonDocument.Parse(MapArtTable.Build("1.2", Capture()).ToJson()).RootElement;

        Assert.Equal(("1.2", MapArtTable.CurrentVersion), (root.GetProperty("gameVersion").GetString(), root.GetProperty("version").GetInt32()));
    }
}
