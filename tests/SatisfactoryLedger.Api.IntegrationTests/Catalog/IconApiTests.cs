using System.Net;
using System.Net.Http.Json;
using System.Text;

using Microsoft.AspNetCore.Mvc;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

/// <summary>
/// Importing the icons the map tool writes, and serving them to the pages (ADR-0032). Made-up images of a few bytes:
/// the real icons are the game's art and never committed.
/// </summary>
public sealed class IconApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string Icon(string className, byte[] image) =>
        $$"""{ "className": "{{className}}", "contentType": "image/png", "image": "{{Convert.ToBase64String(image)}}" }""";

    /// <summary>An item's icon and a building's descriptor's.</summary>
    private static string TwoIcons(string gameVersion = "1.2") =>
        $$"""{ "version": 1, "gameVersion": "{{gameVersion}}", "icons": [ {{Icon("Desc_IronPlate_C", [1, 2, 3])}}, {{Icon("Desc_ConstructorMk1_C", [4, 5, 6])}} ] }""";

    private async Task<HttpResponseMessage> PostAsync(string json)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "file", "icons.json");
        return await _client.PostAsync("/api/v1/catalog/icon-imports", form, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetIconAsync(string className) =>
        await _client.GetAsync($"/api/v1/catalog/icons/{className}", TestContext.Current.CancellationToken);

    [Fact]
    public async Task There_are_no_icons_before_they_are_imported()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/catalog/icons", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Importing_the_icons_is_recorded_as_an_import_of_their_game_version()
    {
        HttpResponseMessage response = await PostAsync(TwoIcons());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken);
        Assert.Equal(("icons", "1.2"), (import!.Source, import.GameVersion));
    }

    [Fact]
    public async Task The_icons_say_which_descriptors_have_one()
    {
        await PostAsync(TwoIcons());

        IconsResult icons = await _client.GetAsync<IconsResult>("/api/v1/catalog/icons");

        Assert.Equal("1.2", icons.GameVersion);
        Assert.Equal(["Desc_ConstructorMk1_C", "Desc_IronPlate_C"], icons.Descriptors);
    }

    [Fact]
    public async Task An_item_s_icon_is_served_as_the_png_it_is()
    {
        await PostAsync(TwoIcons());

        HttpResponseMessage response = await GetIconAsync("Desc_IronPlate_C");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_building_s_icon_is_the_one_of_the_descriptor_it_is_built_from()
    {
        await PostAsync(TwoIcons());

        HttpResponseMessage response = await GetIconAsync("Build_ConstructorMk1_C");

        Assert.Equal([4, 5, 6], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Importing_icons_again_replaces_the_old_whole()
    {
        await PostAsync(TwoIcons("1.1"));

        await PostAsync($$"""{ "version": 1, "gameVersion": "1.2", "icons": [ {{Icon("Desc_IronRod_C", [7])}} ] }""");

        IconsResult icons = await _client.GetAsync<IconsResult>("/api/v1/catalog/icons");
        Assert.Equal("1.2", icons.GameVersion);
        Assert.Equal(["Desc_IronRod_C"], icons.Descriptors);
        Assert.Equal(HttpStatusCode.NotFound, (await GetIconAsync("Desc_IronPlate_C")).StatusCode);
    }

    [Fact]
    public async Task A_file_that_is_not_icons_is_refused_and_the_icons_kept()
    {
        await PostAsync(TwoIcons());

        HttpResponseMessage response = await PostAsync("""{ "version": 2 }""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Catalog.InvalidIcons", problem!.Extensions["code"]?.ToString());
        Assert.Equal(2, (await _client.GetAsync<IconsResult>("/api/v1/catalog/icons")).Descriptors.Count);
    }

    private sealed record IconsResult(string GameVersion, IReadOnlyList<string> Descriptors);
}
