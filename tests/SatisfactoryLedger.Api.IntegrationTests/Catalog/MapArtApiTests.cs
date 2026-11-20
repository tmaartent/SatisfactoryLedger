using System.Net;
using System.Net.Http.Json;
using System.Text;

using Microsoft.AspNetCore.Mvc;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

/// <summary>
/// Importing the map art the map tool writes, and serving it to the map (ADR-0020). Made-up tiles of a few bytes: the
/// real map art is the game's picture and never committed.
/// </summary>
public sealed class MapArtApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string Tile(int column, int row, byte[] image) =>
        $$"""{ "column": {{column}}, "row": {{row}}, "contentType": "image/jpeg", "image": "{{Convert.ToBase64String(image)}}" }""";

    /// <summary>Two tiles side by side, over a world 2,000 cm wide and 1,000 cm high.</summary>
    private static string TwoTiles(string gameVersion = "1.2") =>
        $$"""{ "version": 1, "gameVersion": "{{gameVersion}}", "x0": -1000, "y0": -500, "width": 2000, "height": 1000, "tiles": [ {{Tile(0, 0, [1, 2, 3])}}, {{Tile(1, 0, [4, 5, 6])}} ] }""";

    private async Task<HttpResponseMessage> PostAsync(string json)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "file", "map-art.json");
        return await _client.PostAsync("/api/v1/catalog/map-art-imports", form, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task There_is_no_map_art_before_one_is_imported()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/catalog/map-art", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Importing_the_map_art_is_recorded_as_an_import_of_its_game_version()
    {
        HttpResponseMessage response = await PostAsync(TwoTiles());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken);
        Assert.Equal(("mapArt", "1.2"), (import!.Source, import.GameVersion));
    }

    [Fact]
    public async Task The_map_art_says_where_each_tile_lies_in_the_world_and_where_to_get_it()
    {
        await PostAsync(TwoTiles());

        MapArtResult art = await _client.GetAsync<MapArtResult>("/api/v1/catalog/map-art");

        Assert.Equal("1.2", art.GameVersion);
        Assert.Equal(
            [
                new MapArtTileResult(0, 0, -1000, -500, 0, 500, "/api/v1/catalog/map-art/tiles/0/0"),
                new MapArtTileResult(1, 0, 0, -500, 1000, 500, "/api/v1/catalog/map-art/tiles/1/0"),
            ],
            art.Tiles);
    }

    [Fact]
    public async Task A_tile_is_served_as_the_image_it_is()
    {
        await PostAsync(TwoTiles());

        HttpResponseMessage response = await _client.GetAsync("/api/v1/catalog/map-art/tiles/1/0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([4, 5, 6], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Importing_map_art_again_replaces_the_old_whole()
    {
        await PostAsync(TwoTiles("1.1"));

        await PostAsync($$"""{ "version": 1, "gameVersion": "1.2", "x0": 0, "y0": 0, "width": 100, "height": 100, "tiles": [ {{Tile(0, 0, [7])}} ] }""");

        MapArtResult art = await _client.GetAsync<MapArtResult>("/api/v1/catalog/map-art");
        Assert.Equal(("1.2", 1), (art.GameVersion, art.Tiles.Count));
        HttpResponseMessage gone = await _client.GetAsync("/api/v1/catalog/map-art/tiles/1/0", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task A_file_that_is_not_map_art_is_refused_and_the_map_art_kept()
    {
        await PostAsync(TwoTiles());

        HttpResponseMessage response = await PostAsync("""{ "version": 2 }""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Catalog.InvalidMapArt", problem!.Extensions["code"]?.ToString());
        Assert.Equal(2, (await _client.GetAsync<MapArtResult>("/api/v1/catalog/map-art")).Tiles.Count);
    }

    private sealed record MapArtResult(string GameVersion, double X0, double Y0, double X1, double Y1, IReadOnlyList<MapArtTileResult> Tiles);

    private sealed record MapArtTileResult(int Column, int Row, double X0, double Y0, double X1, double Y1, string Url);
}
