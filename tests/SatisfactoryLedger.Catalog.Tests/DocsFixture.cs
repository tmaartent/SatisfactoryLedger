using SatisfactoryLedger.Catalog.Importer;

namespace SatisfactoryLedger.Catalog.Tests;

/// <summary>A Docs file from <c>Fixtures/</c>, read as production reads an upload: as a stream.</summary>
internal static class DocsFixture
{
    public static async Task<List<GameDataGroupDto>> Import(string path)
    {
        await using FileStream file = File.OpenRead(path);
        return await SatisfactoryImporter.Import(file);
    }
}
