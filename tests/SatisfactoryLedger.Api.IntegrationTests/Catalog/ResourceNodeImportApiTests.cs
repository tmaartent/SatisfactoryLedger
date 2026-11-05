using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

/// <summary>Importing the node table the map tool writes (ADR-0015), and browsing the nodes.</summary>
public sealed class ResourceNodeImportApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Importing_the_node_table_adds_every_node()
    {
        HttpResponseMessage response = await _client.PostResourceNodeImportAsync(File.ReadAllBytes(CatalogClient.ResourceNodeTable));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken);
        Assert.Equal("resourceNodeTable", import!.Source);
        Assert.Equal("1.2", import.GameVersion);
        Assert.Equal(new ImportCounts(577, 0, 0), import.ResourceNodes);
        Assert.Equal(new ImportCounts(0, 0, 0), import.Items);
    }

    [Fact]
    public async Task Importing_the_same_table_again_adds_nothing_and_removes_nothing()
    {
        await _client.ImportResourceNodesAsync();

        ImportResult second = await _client.ImportResourceNodesAsync();

        Assert.Equal(new ImportCounts(Added: 0, Updated: 577, NoLongerInGame: 0), second.ResourceNodes);
        Assert.Empty(second.NoLongerInGame);
    }

    [Fact]
    public async Task Resource_nodes_can_be_browsed_per_resource()
    {
        await _client.ImportResourceNodesAsync();

        var iron = await _client.GetAsync<CatalogResourceNode[]>("/api/v1/catalog/resource-nodes?resource=Desc_OreIron_C");

        Assert.Equal(127, iron.Length);
        CatalogResourceNode node = Assert.Single(iron, node => node.Name == "BP_ResourceNode35");
        Assert.Equal(("resourceNode", "normal", true), (node.Kind, node.Purity, node.InGame));
    }

    [Fact]
    public async Task A_file_that_is_not_a_node_table_is_unprocessable()
    {
        HttpResponseMessage response = await _client.PostResourceNodeImportAsync("""{ "version": 2, "nodes": [] }"""u8.ToArray());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Catalog.InvalidResourceNodeTable", problem!.Extensions["code"]?.ToString());
    }
}
