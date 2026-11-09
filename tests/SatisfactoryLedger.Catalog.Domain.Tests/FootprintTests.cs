namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class FootprintTests
{
    private static readonly FootprintId Constructor = FootprintId.Of("Build_ConstructorMk1_C");

    [Fact]
    public void A_footprint_id_cannot_be_blank()
    {
        Assert.Throws<ArgumentException>(() => FootprintId.Of(" "));
    }

    [Fact]
    public void A_constructor_takes_8_by_10_metres()
    {
        Footprint footprint = Footprint.Around(Constructor, [new ClearanceBox(-400, -500, 400, 500, Soft: false)]);

        Assert.Equal((800m, 1000m), (footprint.Width, footprint.Length));
    }

    [Fact]
    public void A_footprint_encloses_every_box_of_the_building()
    {
        Footprint footprint = Footprint.Around(Constructor,
        [
            new ClearanceBox(-400, -500, 400, 500, Soft: false),
            new ClearanceBox(-100, 500, 100, 700, Soft: false),
        ]);

        Assert.Equal((-400m, -500m, 400m, 700m), (footprint.MinX, footprint.MinY, footprint.MaxX, footprint.MaxY));
    }

    [Fact]
    public void The_room_kept_free_around_a_building_is_not_part_of_its_footprint()
    {
        Footprint footprint = Footprint.Around(Constructor,
        [
            new ClearanceBox(-400, -500, 400, 500, Soft: false),
            new ClearanceBox(-600, -700, 600, 700, Soft: true),
        ]);

        Assert.Equal((800m, 1000m), (footprint.Width, footprint.Length));
    }

    [Fact]
    public void A_building_with_only_soft_boxes_takes_the_room_they_give()
    {
        // A splitter: its one box is soft, the room a belt passes through.
        Footprint footprint = Footprint.Around(FootprintId.Of("Build_ConveyorAttachmentSplitter_C"), [new ClearanceBox(-200, -200, 200, 200, Soft: true)]);

        Assert.Equal((400m, 400m), (footprint.Width, footprint.Length));
    }

    private static ClearanceVolume Volume(decimal height) => new(-400, -500, 0, 400, 500, height, 0, 0, 0, 1, 0, 0, 0);

    [Fact]
    public void A_footprint_keeps_the_volume_of_each_solid_box_for_drawing_the_building_in_3D()
    {
        Footprint footprint = Footprint.Around(Constructor,
        [
            new ClearanceBox(-400, -500, 400, 500, Soft: false) { Volume = Volume(800) },
            new ClearanceBox(-100, 500, 100, 700, Soft: false) { Volume = Volume(300) },
            new ClearanceBox(-600, -700, 600, 700, Soft: true) { Volume = Volume(100) },
        ]);

        Assert.Equal([Volume(800), Volume(300)], footprint.Volumes);
    }

    [Fact]
    public void A_building_with_only_soft_boxes_keeps_their_volumes()
    {
        Footprint footprint = Footprint.Around(FootprintId.Of("Build_ConveyorAttachmentSplitter_C"),
            [new ClearanceBox(-200, -200, 200, 200, Soft: true) { Volume = Volume(200) }]);

        Assert.Equal([Volume(200)], footprint.Volumes);
    }

    [Fact]
    public void A_building_without_boxes_has_no_footprint()
    {
        Assert.Throws<ArgumentException>(() => Footprint.Around(Constructor, []));
    }

    [Fact]
    public void A_footprint_has_a_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Footprint.Create(Constructor, 0, 0, 0, 500));
    }

    [Fact]
    public void A_footprint_from_a_newer_game_file_replaces_the_size()
    {
        Footprint stored = Footprint.Create(Constructor, -400, -500, 400, 500);
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Footprint.Around(Constructor, [new ClearanceBox(-450, -500, 450, 500, Soft: false) { Volume = Volume(900) }]));

        Assert.Equal((900m, true), (stored.Width, stored.IsInGame));
        Assert.Equal([Volume(900)], stored.Volumes);
    }
}
