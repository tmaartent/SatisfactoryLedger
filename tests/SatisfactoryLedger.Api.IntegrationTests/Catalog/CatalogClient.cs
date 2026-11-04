using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

// The response shapes as a client sees them. Kept separate from the API's own types on purpose:
// renaming a field in the API should break these tests, because it breaks the contract.
public sealed record ImportCounts(int Added, int Updated, int NoLongerInGame);

public sealed record ImportResult(
    Guid Id,
    string GameVersion,
    DateTimeOffset ImportedAt,
    ImportCounts Items,
    ImportCounts Buildings,
    ImportCounts TransportTiers,
    ImportCounts Recipes,
    string[] NoLongerInGame);

public sealed record CatalogItem(string Id, string Name, string Form, bool IsRawResource, bool InGame);

public sealed record CatalogRate(decimal PerMinute, string Exact);

public sealed record CatalogIngredient(string ItemId, decimal AmountPerCycle, CatalogRate Rate);

public sealed record CatalogRecipe(
    string Id,
    string Name,
    string BuildingId,
    decimal CycleSeconds,
    bool IsAlternate,
    bool InGame,
    CatalogIngredient[] Inputs,
    CatalogIngredient[] Outputs);

public sealed record CatalogTransportTier(string Id, string Name, string Form, int Capacity, bool InGame);

internal static class CatalogClient
{
    public const string GameFile = "Fixtures/en-US.json";
    public const string SmallFile = "Fixtures/Valid.json";

    public static Task<HttpResponseMessage> PostImportAsync(this HttpClient client, string fixture, string? gameVersion = "1.1")
    {
        return client.PostImportAsync(File.ReadAllBytes(fixture), Path.GetFileName(fixture), gameVersion);
    }

    public static async Task<HttpResponseMessage> PostImportAsync(this HttpClient client, JsonNode docsFile, string? gameVersion = "1.1")
    {
        return await client.PostImportAsync(System.Text.Encoding.UTF8.GetBytes(docsFile.ToJsonString()), "en-US.json", gameVersion);
    }

    private static async Task<HttpResponseMessage> PostImportAsync(this HttpClient client, byte[] docsFile, string fileName, string? gameVersion)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(docsFile), "file", fileName);
        if (gameVersion is not null)
        {
            form.Add(new StringContent(gameVersion), "gameVersion");
        }

        return await client.PostAsync("/api/v1/catalog/imports", form, TestContext.Current.CancellationToken);
    }

    public static async Task<ImportResult> ImportAsync(this HttpClient client, string fixture)
    {
        HttpResponseMessage response = await client.PostImportAsync(fixture);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string url)
    {
        return (await client.GetFromJsonAsync<T>(url, TestContext.Current.CancellationToken))!;
    }
}