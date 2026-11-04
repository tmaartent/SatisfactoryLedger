using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed class CatalogImportApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetCatalogAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Importing_the_game_file_adds_its_whole_catalog()
    {
        HttpResponseMessage response = await _client.PostImportAsync(CatalogClient.GameFile, gameVersion: "1.1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken);
        Assert.Equal("1.1", import!.GameVersion);
        Assert.Equal(new ImportCounts(190, 0, 0), import.Items);
        Assert.Equal(new ImportCounts(11, 0, 0), import.Buildings);
        Assert.Equal(new ImportCounts(8, 0, 0), import.TransportTiers);
        Assert.Equal(new ImportCounts(291, 0, 0), import.Recipes);
    }

    [Fact]
    public async Task An_import_can_be_read_back_from_its_location()
    {
        HttpResponseMessage response = await _client.PostImportAsync(CatalogClient.SmallFile);
        var created = await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken);

        var readBack = await _client.GetAsync<ImportResult>(response.Headers.Location!.ToString());

        Assert.Equal(created!.Id, readBack.Id);
        Assert.Equal(created.Recipes, readBack.Recipes);
    }

    [Fact]
    public async Task Importing_the_same_file_again_adds_nothing_and_removes_nothing()
    {
        await _client.ImportAsync(CatalogClient.SmallFile);

        ImportResult second = await _client.ImportAsync(CatalogClient.SmallFile);

        Assert.Equal(0, second.Items.Added + second.Buildings.Added + second.TransportTiers.Added + second.Recipes.Added);
        Assert.Empty(second.NoLongerInGame);
        Assert.Equal(4, (await _client.GetAsync<CatalogRecipe[]>("/api/v1/catalog/recipes")).Length);
    }

    [Fact]
    public async Task A_recipe_missing_from_a_new_game_file_is_kept_but_no_longer_in_the_game()
    {
        await _client.ImportAsync(CatalogClient.SmallFile);
        JsonNode withoutAlternateWire = RemoveClass(CatalogClient.SmallFile, "Recipe_Alternate_Wire_1_C");

        HttpResponseMessage response = await _client.PostImportAsync(withoutAlternateWire, gameVersion: "1.2");

        var import = await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken);
        Assert.Equal(["Recipe_Alternate_Wire_1_C"], import!.NoLongerInGame);
        var recipes = await _client.GetAsync<CatalogRecipe[]>("/api/v1/catalog/recipes");
        Assert.False(Assert.Single(recipes, recipe => recipe.Id == "Recipe_Alternate_Wire_1_C").InGame);
    }

    [Fact]
    public async Task An_import_without_a_game_version_is_rejected()
    {
        HttpResponseMessage response = await _client.PostImportAsync(CatalogClient.SmallFile, gameVersion: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains("gameVersion", problem!.Errors.Keys);
    }

    [Fact]
    public async Task An_import_sent_as_json_is_an_unsupported_media_type()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/catalog/imports", new { gameVersion = "1.1" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task A_broken_form_upload_is_a_bad_request_not_a_server_error()
    {
        var noBoundary = new StringContent("not a form");
        noBoundary.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");

        HttpResponseMessage response = await _client.PostAsync("/api/v1/catalog/imports", noBoundary, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_file_that_is_not_a_complete_docs_file_is_rejected_with_a_stable_code()
    {
        HttpResponseMessage response = await _client.PostImportAsync("Fixtures/NoBuildingGroup.json");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Catalog.InvalidDocsFile", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_rejected_file_changes_nothing()
    {
        await _client.PostImportAsync("Fixtures/NoBuildingGroup.json");

        Assert.Empty(await _client.GetAsync<CatalogItem[]>("/api/v1/catalog/items"));
    }

    [Fact]
    public async Task An_unknown_import_is_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/catalog/imports/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static JsonNode RemoveClass(string fixture, string className)
    {
        JsonNode docsFile = JsonNode.Parse(File.ReadAllText(fixture))!;
        foreach (JsonNode? group in docsFile.AsArray())
        {
            JsonArray classes = group!["Classes"]!.AsArray();
            JsonNode? match = classes.FirstOrDefault(entry => entry!["ClassName"]!.GetValue<string>() == className);
            if (match is not null)
            {
                classes.Remove(match);
            }
        }

        return docsFile;
    }
}