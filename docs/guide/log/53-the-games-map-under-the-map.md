# Step 53 · The game's map under the map

**Goal:** the game's own map picture under the world map (asked 2026-11-20). Branch: `feat/optimal-arrangement`, on top of step 52 (uncommitted), uncommitted.

## What you decided

- **Where the picture comes from: the map tool**, from your own install, as the resource nodes do (ADR-0015). Not exported by hand with FModel, not a community site's tiles.
- **Where it lives: imported into the Catalog**, stored in PostgreSQL, served by the API. Not a local folder next to the API.
- ADR-0020 had left the game's art out, with "a local-only image could come later". It's amended, and so is ADR-0015.

## The spike: where the picture is, and where it lies

- **The picture:** `FactoryGame/Content/FactoryGame/Interface/UI/Assets/MapTest/SlicedMap/`, four textures `Map_0-0` to `Map_1-1`, 4096 pixels square each, DXT1. `Map_a-b` is column a, row b: put together that way they fit without a seam. The folder also holds a `Map_Test_Gradient`, which the tool leaves out.
- **Where it lies:** the main level has a `BP_MinimapCaptureActor_C`. It stands at (50,301.832, 0), looks straight down, and its camera's `OrthoWidth` is 750,000 cm. So the picture covers x from −324,698.168 to 425,301.832 and y from −375,000 to 375,000. The tool reads these values, it doesn't type them in.
- **Checked:** all 577 resource nodes from your catalog drawn on it in red. Every one lands on land, the well satellites in their craters.

## What changed

- **The map tool** writes the map art when asked: `--map-art map-art.json`, next to the node table. Run on your install: 4 tiles, about 2.2–3 MB each as JPEG, 9.9 MB in all.
  - `GameMap.MapCapture()` reads the capture actor and decodes the textures.
  - `MapArtTable` turns that into our own format: the rectangle and the tiles, each with its column, row and image as base64.
- **The Catalog** keeps it:
  - Domain: `MapArt` (its game version, rectangle and tiles; refused unless the tiles form a full grid, each once, each with an image), `MapArtTile`, `MapArtBounds` (with `Tile(columns, rows, column, row)`: a tile's part of the world). `CatalogImport.RecordMapArt` and the source `mapArt`.
  - `MapArtReader` reads the file. Only JPEG and PNG tiles get in: a tile is served back as the type the file says, so a file can't slip in HTML that the API would then serve.
  - Migration `AddMapArt`: tables `map_art` (one row) and `map_art_tiles` (the images as `bytea`, deleted with their map art).
  - `POST /api/v1/catalog/map-art-imports` replaces the map art whole, in one transaction. `GET /api/v1/catalog/map-art` gives the rectangle and each tile's part of the world and URL (404 before an import), without reading the images. `GET /api/v1/catalog/map-art/tiles/{column}/{row}` serves a tile, cached for a day.
- **The web app:**
  - The Catalog page has a third import card, *The game's map*.
  - The world map lays each tile under everything else (`mapArtTiles` in `map-model.ts`, a Leaflet image overlay per tile), with or without a snapshot loaded. Without map art the map is the plain grid, as before. The grid stays over the picture.
- **The picture is the game's art:** `map-art.json` is in `.gitignore`, and every test uses made-up tiles of a few bytes. Nothing of it is committed.

## Also changed

- **The 3D views' background** (asked 2026-11-20: it was white). The scene took its colour from the CSS variable `--scene`, which is Material's `--mat-sys-surface-container-lowest`. The site's dark colour scheme makes Material's colours `light-dark(…)`, which three.js can't read, so it kept its default white. Now the scene takes the stage's **computed** background colour, which the browser has already resolved to `rgb(…)` (`sceneBackground` in `area/scene-background.ts`, test first). Both 3D views use it: the 3D page, with a factory's page, and the arrangement's belt floor. A fix of its own (`fix(web)`).
- **Aspire 13.6.4 to 13.6.5.** A new high-severity advisory (GHSA-f7vh-h5g3-7jrh, CVE-2026-120006: the Aspire dashboard's cookie can reach a linked container) made the build fail, warnings being errors. The patch release fixes it: `Directory.Packages.props` and the AppHost's SDK. A commit of its own (`chore(deps)`).

## Choices made (overrule if you like)

- **Not `CUE4Parse-Conversion`** for decoding the textures, though it's CUE4Parse's own companion. It pins `SixLabors.ImageSharp` to exactly 3.1.12, which has four high-severity advisories fixed only in 3.2.0. The build refused it. Overriding an exact pin would be guessing at compatibility. The tool decodes the DXT1 with `AssetRipper.TextureDecoder` (MIT, which `CUE4Parse-Conversion` itself uses underneath) and writes JPEG with `SkiaSharp` (MIT), both checked for advisories. It refuses a texture that isn't DXT1.
- **JPEG at quality 85**, about 10 MB for the whole map: sharp enough to read the terrain at the closest zoom, small enough to store and send. PNG would be about 18 MB.
- **The 3D views, the arrangement's map and the factory pages don't show the picture.** Only the world map. Say so if you want it on the arrangement's map too.
- **The tiles are cached for a day** in the browser. After a new import, a reload with an empty cache shows the new picture at once.

## What you need to do

1. Start the AppHost. I stopped it to run the tests; the API needs the new migration and endpoints.
2. Run the tool with `--map-art`, e.g.:
   `dotnet run --project tools/SatisfactoryLedger.MapExtractor -- --game "C:\Program Files (x86)\Steam\steamapps\common\Satisfactory" --game-version 1.2 --out resource-nodes.json --map-art map-art.json`
3. Import `map-art.json` on the Catalog page, under *The game's map*.

## Files

Tool: `GameMap.cs` (`MapCapture`, `Jpeg`, a shared `Read`), new `MapCapture.cs`, `MapImage.cs`, `MapArtTable.cs`, `MapArtTileEntry.cs`, `Program.cs` (`--map-art`), the `.csproj` and `Directory.Packages.props` (`AssetRipper.TextureDecoder`, `SkiaSharp`; Aspire 13.6.5). Catalog: `Domain/Map/MapArt.cs`, `MapArtTile.cs`, `MapArtBounds.cs`; `CatalogImport.RecordMapArt`, `CatalogImportSource.MapArt`; `GameData/MapArtFile/MapArtReader.cs`; `Imports/ImportMapArtHandler.cs`, `ImportMapArtCommand.cs`, `InvalidMapArtException.cs`, `ImportEndpoints.cs`; `Browsing/MapArtEndpoints.cs`, `MapArtResponse.cs`, `MapArtTileResponse.cs`; `Persistence/Configurations/MapArtConfiguration.cs`, `CatalogDbContext.cs`, migration `AddMapArt`; `CatalogModule.cs`; `api/openapi.json`. Tests: `MapArtTests`, `CatalogImportTests`, `MapArtTableTests`, `MapArtReaderTests`, `MapArtApiTests`. Web: `catalog/catalog-page.ts`, `.html` and new `.spec.ts`; `world/map/map-model.ts` (`mapArtTiles`) and spec; `world/map/map-page.ts` and spec; `world/area/scene-background.ts` and spec, `area/area-page.ts`, `arrangement/belt-floor-3d.ts`. Docs: ADR-0015 and ADR-0020 amended, glossary *Map Art*, primer (two rows), `.gitignore`.

1055 .NET tests and 191 web tests green, builds clean. Written test first, seen failing: the domain's, the reader's (not compiling), the API's (500, then the execution strategy fixed), the web's. `MapArtTableTests` were written with their code, not run red first. Not seen in a browser.

## .NET / web points

| Point | Java / TypeScript |
|---|---|
| **A retrying execution strategy forbids your own transactions.** Aspire's Npgsql retries failed commands; `BeginTransactionAsync` then throws. The whole transaction goes inside `CreateExecutionStrategy().ExecuteAsync(...)`, so a retry repeats all of it (primer, *Backend*). | Spring's `@Retryable` around a `@Transactional` method: the retry has to wrap the transaction, never sit inside it. |
| **Get-only properties aren't mapped by convention.** `public double X0 { get; }` was skipped until named in the configuration; then EF Core passes it to the constructor (primer, *Backend*). | JPA maps fields and doesn't need setters. |
| **Projections keep big columns in the database.** `GET /map-art` selects only `Column` and `Row` of the owned tiles; EF Core reads no `bytea`. Loading the `MapArt` entity would read all 10 MB. | A JPQL constructor expression, `select new …(t.column, t.row)`, or a Spring Data projection interface. |
| **`ExecuteDeleteAsync`** deletes in the database with one `DELETE`, without loading the rows; the foreign key's cascade takes the tiles. | JPQL bulk `delete from MapArt`, which also skips the entity lifecycle. |
| **A transitive package with an exact pin.** `[3.1.12]` in a `.nuspec` is an exact version; NuGet won't take a newer one without a warning, unlike a plain minimum. | Maven's `[3.1.12]` version range means the same. |
| **`resource.value()` throws in the error state**, so a 404 that means "none yet" is turned into `null` in the loader, and only other errors stay errors. | A Spring `RestClient` `onStatus(404, …)` handler returning empty. |
