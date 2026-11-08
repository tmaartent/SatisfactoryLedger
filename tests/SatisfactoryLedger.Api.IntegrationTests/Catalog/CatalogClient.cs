using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

internal static class CatalogClient
{
    public const string GameFile = "Fixtures/en-US.json";
    public const string SmallFile = "Fixtures/Valid.json";

    /// <summary>The node table the map tool wrote from a 1.2 install (ADR-0015).</summary>
    public const string ResourceNodeTable = "Fixtures/resource-nodes.json";

    public static async Task<HttpResponseMessage> PostResourceNodeImportAsync(this HttpClient client, byte[] table)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(table), "file", "resource-nodes.json");
        return await client.PostAsync("/api/v1/catalog/resource-node-imports", form, TestContext.Current.CancellationToken);
    }

    public static async Task<ImportResult> ImportResourceNodesAsync(this HttpClient client)
    {
        HttpResponseMessage response = await client.PostResourceNodeImportAsync(File.ReadAllBytes(ResourceNodeTable));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportResult>(TestContext.Current.CancellationToken))!;
    }

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