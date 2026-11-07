using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tests;

/// <summary>A dimensional depot uploader in a stored snapshot (BR-A8): it takes only what's left over.</summary>
public class UploaderSnapshotTests
{
    private static readonly CopiedCatalog NoCatalog = new(
        new Dictionary<string, RecipeDocument>(), new Dictionary<string, ExtractorDocument>(), new Dictionary<string, TransportTierDocument>(),
        new Dictionary<string, ResourceNodeDocument>(), new Dictionary<string, GeneratorDocument>());

    private static Consumer ConsumerOf(BuildableDocument document) => Assert.IsType<Consumer>(document.ToBuildable(NoCatalog));

    [Fact]
    public void An_uploader_marked_by_the_save_reader_takes_only_what_is_left_over()
    {
        var uploader = new BuildableDocument("uploader", BuildableDocumentRole.Consumer, "Build_CentralStorage_C", TakesLeftoversOnly: true);

        SnapshotDocument stored = SnapshotDocument.FromJson((TestWorldSnapshot.Document with { Buildables = [uploader] }).ToJson());

        Assert.True(ConsumerOf(stored.Buildables[0]).TakesLeftoversOnly);
    }

    [Fact]
    public void An_awesome_sink_takes_everything()
    {
        Assert.False(ConsumerOf(new BuildableDocument("sink", BuildableDocumentRole.Consumer, "Build_ResourceSink_C")).TakesLeftoversOnly);
    }
}
