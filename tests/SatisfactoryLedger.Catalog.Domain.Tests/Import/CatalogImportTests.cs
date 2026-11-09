using SatisfactoryLedger.Catalog.Domain.GameObjects;
using SatisfactoryLedger.Catalog.Domain.Import;
using SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.Import;

public class CatalogImportTests
{
    private static readonly DateTimeOffset ImportedAt = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_entry_only_in_the_game_file_is_added()
    {
        var changes = CatalogEntries.Reconcile(stored: [], fromGame: [TestCatalog.Items.IronPlate]);

        Assert.Equal([TestCatalog.Items.IronPlate], changes.Added);
        Assert.Empty(changes.Updated);
        Assert.Empty(changes.NoLongerInGame);
    }

    [Fact]
    public void A_stored_entry_that_is_in_the_game_file_is_updated_from_it()
    {
        var stored = Item.Create(ItemId.Of("Desc_IronPlate_C"), "Old name", ItemForm.Solid, false);

        var changes = CatalogEntries.Reconcile([stored], [TestCatalog.Items.IronPlate]);

        Assert.Same(stored, Assert.Single(changes.Updated));
        Assert.Equal("Iron Plate", stored.Name);
        Assert.Empty(changes.Added);
    }

    [Fact]
    public void A_stored_entry_missing_from_the_game_file_is_no_longer_in_the_game()
    {
        var stored = TestCatalog.Items.IronPlate;

        var changes = CatalogEntries.Reconcile([stored], fromGame: []);

        Assert.Same(stored, Assert.Single(changes.NoLongerInGame));
        Assert.False(stored.IsInGame);
    }

    [Fact]
    public void An_entry_that_was_already_no_longer_in_the_game_is_not_reported_again()
    {
        var stored = TestCatalog.Items.IronPlate;
        stored.MarkNoLongerInGame();

        var changes = CatalogEntries.Reconcile([stored], fromGame: []);

        Assert.Empty(changes.NoLongerInGame);
    }

    [Fact]
    public void Importing_the_same_game_file_twice_changes_nothing_the_second_time()
    {
        Item[] stored = [TestCatalog.Items.IronIngot, TestCatalog.Items.IronPlate];

        var changes = CatalogEntries.Reconcile(stored, [TestCatalog.Items.IronIngot, TestCatalog.Items.IronPlate]);

        Assert.Empty(changes.Added);
        Assert.Empty(changes.NoLongerInGame);
        Assert.All(stored, item => Assert.True(item.IsInGame));
    }

    [Fact]
    public void A_game_file_with_the_same_entry_twice_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CatalogEntries.Reconcile([], [TestCatalog.Items.IronPlate, TestCatalog.Items.IronPlate]));
    }

    [Fact]
    public void An_import_records_the_game_version_and_when_it_happened()
    {
        var import = RecordImport("1.1");

        Assert.Equal("1.1", import.GameVersion);
        Assert.Equal(ImportedAt, import.ImportedAt);
    }

    [Fact]
    public void An_import_needs_a_game_version()
    {
        Assert.Throws<ArgumentException>(() => RecordImport(" "));
    }

    [Fact]
    public void An_import_counts_what_it_changed_per_kind_of_entry()
    {
        var import = RecordImport("1.1",
            items: CatalogEntries.Reconcile([TestCatalog.Items.IronOre, TestCatalog.Items.Wire], [TestCatalog.Items.IronOre, TestCatalog.Items.IronPlate]));

        Assert.Equal(new ImportCounts(Added: 1, Updated: 1, NoLongerInGame: 1), import.Items);
        Assert.Equal(new ImportCounts(0, 0, 0), import.Recipes);
    }

    [Fact]
    public void An_import_lists_the_class_names_that_are_no_longer_in_the_game()
    {
        var import = RecordImport("1.1",
            items: CatalogEntries.Reconcile([TestCatalog.Items.Wire], fromGame: []));

        Assert.Equal(["Desc_Wire_C"], import.NoLongerInGame);
    }

    [Fact]
    public void An_import_counts_and_lists_the_extractors_it_changed()
    {
        var minerMk1 = Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1", ItemForm.Solid, 1, 1);
        var minerMk2 = Extractor.Create(ExtractorId.Of("Build_MinerMk2_C"), "Miner Mk.2", ItemForm.Solid, 1, 0.5m);

        var import = RecordImport("1.2", extractors: CatalogEntries.Reconcile([minerMk1], [minerMk2]));

        Assert.Equal(new ImportCounts(Added: 1, Updated: 0, NoLongerInGame: 1), import.Extractors);
        Assert.Equal(["Build_MinerMk1_C"], import.NoLongerInGame);
    }

    [Fact]
    public void An_import_counts_the_footprints_it_changed()
    {
        var constructor = Footprint.Create(FootprintId.Of("Build_ConstructorMk1_C"), -400, -500, 400, 500);
        var widerConstructor = Footprint.Create(FootprintId.Of("Build_ConstructorMk1_C"), -450, -500, 450, 500);

        var import = RecordImport("1.2", footprints: CatalogEntries.Reconcile([constructor], [widerConstructor]));

        Assert.Equal(new ImportCounts(Added: 0, Updated: 1, NoLongerInGame: 0), import.Footprints);
    }

    [Fact]
    public void A_building_no_longer_in_the_game_is_listed_once_though_its_footprint_went_too()
    {
        var extractor = Extractor.Create(ExtractorId.Of("Build_MinerMk1_C"), "Miner Mk.1", ItemForm.Solid, 1, 1);
        var footprint = Footprint.Create(FootprintId.Of("Build_MinerMk1_C"), -300, -500, 300, 900);

        var import = RecordImport("1.2",
            extractors: CatalogEntries.Reconcile([extractor], fromGame: []),
            footprints: CatalogEntries.Reconcile([footprint], fromGame: []));

        Assert.Equal(["Build_MinerMk1_C"], import.NoLongerInGame);
    }

    [Fact]
    public void An_import_counts_the_drone_ports_it_changed()
    {
        var port = DronePort.Create(DronePortId.Of("Build_DroneStation_C"), "Drone Port", 24_000, 6, []);

        var import = RecordImport("1.2", dronePorts: CatalogEntries.Reconcile([], [port]));

        Assert.Equal(new ImportCounts(Added: 1, Updated: 0, NoLongerInGame: 0), import.DronePorts);
    }

    [Fact]
    public void An_import_counts_the_generators_it_changed()
    {
        var generator = Generator.Create(GeneratorId.Of("Build_GeneratorCoal_C"), "Coal-Powered Generator", 75, 10, []);

        var import = RecordImport("1.2", generators: CatalogEntries.Reconcile([], [generator]));

        Assert.Equal(new ImportCounts(Added: 1, Updated: 0, NoLongerInGame: 0), import.Generators);
    }

    [Fact]
    public void An_import_of_the_docs_file_says_so()
    {
        Assert.Equal(CatalogImportSource.DocsFile, RecordImport("1.2").Source);
    }

    [Fact]
    public void An_import_of_the_node_table_counts_and_lists_the_nodes_it_changed()
    {
        var position = new MapPosition(0, 0, 0);
        ResourceNode Node(string name) => ResourceNode.Create(ResourceNodeId.Of(name), ResourceNodeKind.ResourceNode, ItemId.Of("Desc_OreIron_C"), Purity.Normal, position);

        var import = CatalogImport.RecordResourceNodes(CatalogImportId.New(), "1.2", ImportedAt,
            CatalogEntries.Reconcile([Node("BP_ResourceNode1"), Node("BP_ResourceNode2")], [Node("BP_ResourceNode2"), Node("BP_ResourceNode3")]));

        Assert.Equal(CatalogImportSource.ResourceNodeTable, import.Source);
        Assert.Equal(new ImportCounts(Added: 1, Updated: 1, NoLongerInGame: 1), import.ResourceNodes);
        Assert.Equal(new ImportCounts(0, 0, 0), import.Items);
        Assert.Equal(["BP_ResourceNode1"], import.NoLongerInGame);
    }

    private static CatalogImport RecordImport(
        string gameVersion,
        CatalogEntryChanges<Item>? items = null,
        CatalogEntryChanges<Extractor>? extractors = null,
        CatalogEntryChanges<Footprint>? footprints = null,
        CatalogEntryChanges<Generator>? generators = null,
        CatalogEntryChanges<DronePort>? dronePorts = null) =>
        CatalogImport.RecordDocs(
            CatalogImportId.New(),
            gameVersion,
            ImportedAt,
            items ?? CatalogEntries.Reconcile<Item>([], []),
            CatalogEntries.Reconcile<Building>([], []),
            CatalogEntries.Reconcile<TransportTier>([], []),
            CatalogEntries.Reconcile<Recipe>([], []),
            extractors ?? CatalogEntries.Reconcile<Extractor>([], []),
            footprints ?? CatalogEntries.Reconcile<Footprint>([], []),
            generators ?? CatalogEntries.Reconcile<Generator>([], []),
            dronePorts ?? CatalogEntries.Reconcile<DronePort>([], []));
}