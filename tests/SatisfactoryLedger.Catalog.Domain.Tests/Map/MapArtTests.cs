using SatisfactoryLedger.Catalog.Domain.Map;

namespace SatisfactoryLedger.Catalog.Domain.Tests.Map;

public class MapArtTests
{
    // The game's map: 750,000 cm square around the capture actor at (50,301.832, 0), in 2 by 2 tiles.
    private const double X0 = -324698.168;
    private const double Y0 = -375000;
    private const double Size = 750000;

    private static MapArtTile Tile(int column, int row) => new(column, row, [1, 2, 3], "image/jpeg");

    private static MapArt GameMap(params MapArtTile[] tiles) =>
        MapArt.Create("1.2", X0, Y0, Size, Size, tiles.Length > 0 ? tiles : [Tile(0, 0), Tile(1, 0), Tile(0, 1), Tile(1, 1)]);

    [Fact]
    public void The_map_art_covers_the_world_rectangle_the_game_captured()
    {
        MapArt art = GameMap();

        Assert.Equal(new MapArtBounds(X0, Y0, X0 + Size, Y0 + Size), art.Bounds);
    }

    [Fact]
    public void Each_tile_covers_its_part_of_the_world_columns_along_x_and_rows_along_y()
    {
        MapArtBounds world = GameMap().Bounds;

        Assert.Equal(new MapArtBounds(X0 + (Size / 2), Y0, X0 + Size, Y0 + (Size / 2)), world.Tile(columns: 2, rows: 2, column: 1, row: 0));
    }

    [Fact]
    public void Map_art_with_a_tile_missing_from_its_grid_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GameMap(Tile(0, 0), Tile(1, 0), Tile(0, 1)));
    }

    [Fact]
    public void Map_art_with_a_tile_twice_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GameMap(Tile(0, 0), Tile(0, 0)));
    }

    [Fact]
    public void Map_art_without_an_image_in_a_tile_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GameMap(new MapArtTile(0, 0, [], "image/jpeg")));
    }

    [Fact]
    public void Map_art_covering_no_ground_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapArt.Create("1.2", X0, Y0, 0, Size, [Tile(0, 0)]));
    }

    [Fact]
    public void Map_art_belongs_to_a_game_version()
    {
        Assert.Throws<ArgumentException>(() => MapArt.Create(" ", X0, Y0, Size, Size, [Tile(0, 0)]));
    }
}
