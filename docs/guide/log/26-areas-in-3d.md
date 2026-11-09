# Step 26 · An area of the map in 3D

**Goal:** draw a rectangle around some machines on the map, and see everything inside it in 3D: machines as their real shapes, belts and pipes along their curves, each machine coloured by the analysis, with its recipe, stock and findings one click away. Step one of two; selecting a factory in this view comes next.

## What you decided

- **The flow:** map → draw a rectangle → 3D view of what's inside → inspect machines. Later: select the factory's machines there.
- **Two steps,** this one read-only.
- **For step 2** (to confirm then): a selection of machines is turned into today's definition, with **several outputs** allowed. Belts are members because the trace finds them; the output point is the end of the line (a container, a station, or another factory's machine), so a long belt to storage belongs to the factory.

## How to use it

1. Import the **Docs file** again: the Catalog only now keeps each building's 3D shape (the migration gives existing footprints none).
2. Import your **save** again: a snapshot copies the shapes at import (snapshot version 10). Older snapshots still work, with buildings as flat footprints raised to 4 m.
3. On the map, click **Open an area in 3D**, then drag a rectangle around the factory.
4. In 3D: drag to turn, scroll to zoom, right-drag to move. Click a machine for its panel. The **Show up to** slider hides everything above a height, to look at one floor from above.

## How it works

```
Docs file → ClearanceDataParser: each box twice, flat (for the map) and as a volume (3D, as the file has it)
Catalog   → Footprint.Volumes (jsonb) → CatalogFootprint.Volumes
Snapshot  → FootprintDocument.Volumes (version 10) → map API
Web       → map page: draw a rectangle → /snapshots/{id}/area?x0&y0&x1&y1
            area page: filter the map response by the rectangle, three.js scene, click to inspect
```

- **A volume** is one clearance box as the Docs file gives it: corners in its own frame, a rotation quaternion, an offset. The web app turns it into 8 corners (`volumeCorners`), then turns those by the building's yaw and places them (`buildingBoxes`).
- **Axes:** the game is left-handed with z up, three.js right-handed with y up; swapping y and z does both and keeps north at the top (`toScene`).
- **Fallbacks:** no volumes → the flat footprint raised to 4 m; no footprint at all (lifts) → a 2 m cube.
- **Speed:** belts and pipes are merged per kind and per metre of their lowest point, so a big area stays fast and the height cut-off still applies to them (your review: at first they were merged per kind only and ignored the slider). Buildings stay one mesh each, so a click can find them.
- **The panel** reuses the findings' wording (`limitText`): every input a machine is short of, every product that can't leave, or what it runs on from stock.

## Files

| Where | What |
|---|---|
| `Catalog.Domain/ClearanceVolume.cs`, `ClearanceBox.cs`, `Footprint.cs` | A box's volume; a footprint keeps the volumes of the boxes its rectangle covers |
| `Catalog/GameData/Translator/ClearanceDataParser.cs` | Each box's volume, exactly as the file writes it |
| `Catalog/Persistence/FootprintConfiguration.cs`, `Migrations/…_AddFootprintVolumes.cs` | One jsonb column, `[]` for footprints imported before |
| `Catalog.Contracts/CatalogFootprint.cs`, `CatalogVolume.cs`, `Queries/CatalogQueries.cs` | The volumes in the contract |
| `World/Snapshots/FootprintDocument.cs`, `VolumeDocument.cs`, `SnapshotDocument.cs` | Copied at import, version 10 |
| `web/src/app/world/area/area-model.ts` | What lies in an area, box corners, the axis swap, 3D curves |
| `web/src/app/world/area/area-page.*` | The 3D page |
| `web/src/app/world/map/map-page.*`, `map-model.ts` | Drawing the rectangle (`fromMapPoint`) |
| `web/ng-openapi-gen.json` | `ignoreUnusedModels: false` |
| `docs/adr/0024-areas-in-3d-with-threejs.md` | The decision |

## Points worth noticing

| .NET / Angular / TypeScript | Java / Spring | Here |
|---|---|---|
| EF Core's generated default for a new jsonb list column was `"{}"`, an object, not `"[]"` | A Flyway script is written by hand, so you'd write `'[]'` | The migration was corrected; reading old rows would otherwise have failed |
| `untracked(() => …)` reads signals inside an effect without depending on them | Reading a value without subscribing | `AreaPage.draw` re-applies the cut-off and glow. In the primer. |
| ng-openapi-gen skipped `VolumeDocument`, used only in a nullable list | OpenAPI Generator writes every schema by default | `ignoreUnusedModels: false`. In the primer. |
| three.js in a component: built in `afterNextRender`, redrawn from an `effect`, disposed on destroy | Like a Swing or JavaFX canvas you drive yourself | `AreaPage.start`, `draw`, `stop` |

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `FootprintTests` | Keeps the volume of each solid box · with only soft boxes keeps theirs · a newer file replaces them |
| `SatisfactoryTranslatorTests` | The collider keeps its 23 solid volumes, as the file has them |
| `CatalogQueriesTests` | A footprint comes with its volumes |
| `SnapshotDocumentTests`, `WorldMapTests` | Copies each footprint's volumes · version 10 · a version 9 snapshot still reads · the map returns them |
| Web `area-model.spec.ts` | An area is the same whichever corner it starts from · holds what stands inside it at any height · a belt with a point inside · a box untouched · turned by its quaternion then moved · a building turned by its yaw and placed · flat footprint raised to 4 m · a 2 m cube without a footprint · the axis swap · a curve sampled with heights |
| Web `map-model.spec.ts` | A map point back to a game position |

Red first: the Catalog and snapshot tests failed to compile until the volumes existed; the web spec until `area-model.ts` existed. The three.js drawing itself isn't unit-tested (no WebGL in the test runner): check it in the browser. Totals: 736 .NET tests and 57 web tests.

## Choices worth a second look

1. **Flat footprints raised to 4 m** when a snapshot has no volumes.
2. **The whole map response** is loaded and filtered in the browser, as the map does. Fine for your world; an endpoint per area if worlds grow much bigger.
3. **Belts can't be clicked** (they're merged for speed).
4. **The cut-off:** buildings, belts and pipes whose base is above it are hidden; belts and pipes that climb higher are clipped at the roof of the tallest building still shown (a three.js clipping plane on their materials only). Your review found vertical pipes sticking out of the top floor before that.
5. **Not from npm audit:** a high-severity advisory in `vite` (Angular's dev server) was already there; `npm audit fix` in a separate change.

## Next

Step 2: select machines in the 3D view (click, or a rectangle drawn on the screen), then turn the selection into a factory definition with several outputs, by the rule above.
