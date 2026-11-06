# Step 21 · The world map

**Goal:** see a snapshot instead of reading tables. Where machines stand, how they run, how belts run, where the bottlenecks are.

**Result:** a map per snapshot (`/snapshots/:id/map`) with:
- every building as the ground it covers, turned the way it faces;
- machines coloured by how they run;
- belts and pipes as built, bottlenecks red;
- providers, free resource nodes, and splitters, lifts, storage and consumers;
- a factory highlighted on request.

Snapshots can be deleted from the UI. **Tests:** 599 .NET and 27 web tests, all green.

**Decisions you made:**
- **ADR-0020:**
  - what the map shows;
  - a plain grid behind it;
  - its own map endpoint;
  - belts drawn now (after you asked whether belts keep their shape);
  - footprints as a new Catalog entry, read from the Docs file.
- **Snapshot delete:** the world's factories stay when its last snapshot is deleted.

**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
Catalog        Footprint (new entry): the rectangle around a building's clearance boxes (mClearanceData)
               · ClearanceDataParser · imports count footprints · GetFootprintsAsync on ICatalogQueries · migration
Save reader    a belt's or pipe's curve (mSplineData) in world coordinates · which way a buildable faces (yaw)
Snapshot       version 5: curves, yaws, and the footprints of the classes it has
World          GET /snapshots/{id}/map: machines, providers, conveyors with curves, other logistics, consumers, footprints
               · DELETE /snapshots/{id}
Web            world/map: Leaflet on a 1 km grid · map-model (positions, curves, footprints) · delete with confirmation
```

## How it works

- **Coordinates.** One map unit is a metre. The game's y grows southwards, so the map's latitude is −y and north is up. You checked the orientation against the game.
- **Curves.** Every belt and pipe keeps its curve: points with the tangents it leaves and arrives with. Between two points it's a cubic Hermite spline, and the web app samples it: a straight stretch as its two ends, a bend in 8 steps.
  - The build mode (straight, curved) isn't saved, only the resulting curve.
  - Lifts have no curve.
- **Footprints.** The Docs file has clearance boxes per building class, in the building's own coordinates.
  - The Catalog keeps the rectangle around the hard boxes, each moved and turned by its offset.
  - Soft boxes, room kept free around a building, count only when there's nothing else, as for a splitter.
  - Snapshots copy the footprints of the classes they have, and the map turns each by the building's yaw.
  - A class without one, a lift, is a dot.
- **Status.** The map takes how machines run from the findings, so it draws them while the analysis is pending, grey, and colours them once it's ready.
- **Delete.** `ExecuteDeleteAsync` deletes the snapshot in the database without loading its large document. A cascading foreign key takes the analysis with it. If the analysis service is still working on it, it gets a 404 and skips it.

## What it took (the surprises)

1. **"The save has no belt curves" was wrong.** I had checked our snapshot model, not the save. You asked; the save has `mSplineData` per belt and pipe. ADR-0020 was corrected.
2. **Belts floated between dots.** Belts end at connectors on the edge of a building, not at its centre, and splitters and lifts weren't drawn. Hence the footprints, and drawing the other logistics.
3. **The sizes weren't a table from memory.** The Docs file has each building's clearance boxes, so footprints come from the game's own data.
   - They come with offsets (a miner's boxes sit 2 m to one side), several boxes per building (up to 24), soft boxes, and a few turned boxes.
   - The smelter's connectors sit 2 to 3 m from its centre, inside its 5 by 10 m box.
4. **A water extractor has a "node" too:** the water volume it stands in.
5. **A file-based C# app** (`dotnet run probe.cs`, .NET 10) was enough to look inside the save with the parser library, without a project. Reflection-based JSON is off there, so values were printed by hand.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `FootprintTests` (Catalog domain) | A constructor takes 8 by 10 m · a footprint encloses every box · soft room is left out · a building with only soft boxes takes their room · no boxes, no footprint · a footprint has a size · a newer game file replaces the size |
| `SatisfactoryTranslatorTests` | A constructor from the game file is 8 by 10 m · a miner's boxes are moved by their offsets, its soft box left out · a splitter covers its soft box · footprints come from every kind of building, not lifts · a turned box covers its turned ground |
| `CatalogImportTests` | An import counts the footprints it changed · a building that left the game is listed once |
| `CatalogQueriesTests` | A footprint is found by its building's class name |
| `SaveFileReaderTests` | A straight belt runs from where it stands to its end · a belt keeps every curve point with its tangents · a pipe keeps its curve · splitters, mergers and lifts have none · a turned belt turns its curve (mutation-checked) · a buildable keeps which way it faces · a quarter turn is a yaw of 90° |
| `SnapshotDocumentTests` | A belt keeps its curve · a buildable keeps which way it faces · a snapshot copies the footprints of the buildings it has · a version 4 snapshot reads without curves |
| `WorldMapTests` | Every machine, where it stands · a miner shows its node's resource and purity · a water extractor shows water and no purity · a station without a resource · a belt with its curve · a pipe as a pipe · splitters and lifts aren't conveyors · a machine faces the way it was built · splitters, mergers and lifts are on the map · a sink is a consumer · the map has the footprints |
| `WorldMapApiTests`, `WorldSnapshotApiTests` | The map has machines and providers · each belt with its curve · an unknown snapshot has no map · a deleted snapshot is gone with its findings · an unknown one can't be deleted · a world's factories stay when its last snapshot is deleted |
| Web: `map-model.spec.ts` | Metres with north up · statuses from the findings · unknown without findings · free nodes · a straight belt is a line · a bend follows its curve · a constructor is 8 by 10 m · a footprint turns with its building · an off-centre footprint stays where it is |
| Web: `snapshots-page.spec.ts` | Deletes after you confirm · keeps the snapshot when you cancel |

## .NET / Angular points worth noticing (in the primer)

| Here | Java/Spring | Note |
|---|---|---|
| `afterNextRender`, `viewChild`, `effect` driving Leaflet | `@PostConstruct` for the view | Leaflet needs a real element and knows nothing about signals: effects redraw its layers. |
| `:host ::ng-deep` | — | Reaching HTML a library creates (Leaflet's tooltips) from scoped component styles. |
| `allowedCommonJsDependencies` | — | Leaflet 1.9 is CommonJS. |
| `[GeneratedRegex]` | A precompiled `Pattern` | Used in the clearance data parser. |
| `ExecuteDeleteAsync` | `@Modifying @Query("delete …")` | Deletes in the database without loading entities. |
| `dotnet ef migrations add AddFootprints` | Flyway script | A new table, plus count columns that default to 0 for older imports. |

## Choices worth a second look

1. **Footprints from hard boxes.** Some buildings' hard boxes include parts above ground (a miner's drill), so a footprint can be a little larger than the base.
2. **Only the yaw is kept,** not tilt: the map is seen from above.
3. **Curves are rounded to 0.01 cm** in the snapshot.
4. **The map endpoint is a few MB** for a big world. Speed was fine, so there's no compression yet.
5. **Postman:** the delete request is in a last folder, "5 · Clean up", so a full run deletes the snapshot only after everything else used it.
6. **Older snapshots and catalogs** have no curves, yaws or footprints until the Docs file and the save are imported again.

## Next

What's on belts, lifts, splitters and mergers, and inside machines, at save time (step 22).
