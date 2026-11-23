# Step 71 · The game's icons

**Goal:** the game's icons beside every item and building named, and on the map's resource nodes (asked 2026-11-23). Branch `feat/game-icons`, on top of #18, uncommitted.

## What you decided

- **From the map tool and imported into the Catalog**, as the map picture: from your install, never committed.
- **Everything at once:** items, buildings and the map's nodes.
- **A free node is its resource's icon in a ring of its purity's colour.**
- **256-pixel PNGs.**

Written into a new ADR-0032, with ADR-0015 amended and the glossary's *Icon / Icon Set*.

## The spike

A throwaway option in the map tool loaded every icon the Docs file names (`mPersistentBigIcon`, 741 textures for 747 descriptors):
- all load with CUE4Parse;
- DXT5 (631) or plain BGRA (110), 512 or 256 pixels square, and one 8×8;
- every building that matters pairs with a descriptor of its name: 516 of 546, the rest decorations and the like.

## How it's built

- **The map tool** (`--icons icons.json`):
  - `DocsIcons.Read` gives each descriptor's texture path from the game's Docs file (UTF-16, read from `CommunityResources/Docs/en-US.json`);
  - `GameMap.Icons` loads them, decodes DXT5 (`Bc3`) or takes BGRA as it is, scales to 256 when larger (Mitchell) and writes PNG;
  - `IconTable` / `IconEntry` / `GameIcon` write our own format, version 1.
  - Run on your install: 747 icons in 17 seconds, 56 MB.
- **The Catalog:**
  - Domain: `IconSet` (one at a time: a game version, each descriptor's icon once, each with an image) and `Icon`. `IconSet.DescriptorOf` gives a building its descriptor. `CatalogImport.RecordIcons`, source `icons`.
  - `IconFileReader` reads the file; only PNG gets in.
  - `ImportIconsHandler` replaces the set in one transaction; migration `AddIcons` (`icon_set`, `icons`, images as `bytea`).
  - `POST /icon-imports` (up to 200 MB), `GET /icons` (which descriptors), `GET /icons/{className}` (the PNG, a day's cache; a building's class gives its descriptor's).
- **The web app:**
  - `CatalogNames.icon(className)` and `iconOfItemNamed(name)`: a URL, or null. Loaded apart from the names, so a 404 before an import changes nothing else.
  - `GameIcon` (`<app-game-icon [of]="…" />`), 20 px, beside each name in the Catalog, findings, factory page, 3D view's panel, snapshot page, and (by name) the explanation's starts.
  - The map's tooltips get a 16 px icon (`iconTag`); a free node is a Leaflet marker (`nodeMarkerHtml`), styled in `styles.scss` as Leaflet puts markers outside the page's component.
  - The Catalog page has a fourth import card, *The game's icons*.

### Technical choices to overrule

- **Not inside sentences** (*Not enough Iron Ingot*), nor in the arrangement's tooltips.
- **A provider on a node** (a miner) keeps its shape; only free nodes are icons.
- **20 px beside names, 16 px in tooltips, 22 px rings** on the map.
- **The tool reads the English Docs file** for the icon paths, as the Catalog does.

## Tests

| File | What |
|---|---|
| `IconSetTests` (new, 6) | Each descriptor's icon once; one without an image, or without a game version, refused; an item's icon is its own, a building's its descriptor's |
| `CatalogImportTests` (1 new) | An icon import records its game version and changes no entries |
| `IconFileReaderTests` (new, 3) | Reads each icon; another format version, or an icon not PNG, refused |
| `IconApiTests` (new, 7) | None before an import; recorded as an import; which have one; an item's PNG; a building's is its descriptor's; a new import replaces the old; a wrong file is refused and the old kept |
| `DocsIconsTests`, `IconTableTests` (new, 4) | The texture path from the Docs file, classes without an icon left out; the file's format, sorted, base64 |
| `catalog-names.spec.ts` (5 new) | An item's icon; a building's by its descriptor; none for what has none; none before an import, names still there; by an item's name |
| `game-icon.spec.ts` (new, 3) | The icon as decoration; by name and size; nothing without one |
| `map-model.spec.ts`, `map-page.spec.ts`, `findings.spec.ts`, `catalog-page.spec.ts` | A tooltip's icon tag; the node's marker; a node drawn as its icon in its purity's ring; a machine's building icon in the findings; importing the icons |

1163 .NET tests and 281 web tests pass, both exit code 0; the build is clean (465 kB), and the OpenAPI contract is regenerated. **Imported for real** on your AppHost: 747 icons in 4 seconds; `GET /icons/Build_ConstructorMk1_C` gives the Constructor's PNG. Not looked at in a browser. Written test first except the map tool's `GameMap.Icons`, which needs the game.

## .NET notes

- **Request size limits** (primer): the icons file is 56 MB, over Kestrel's 30 MB default; the endpoint raises it with `RequestSizeLimitAttribute` as metadata.
- **`SKBitmap.Resize` with `SKSamplingOptions(SKCubicResampler.Mitchell)`**: SkiaSharp 3+ takes sampling options instead of the old `SKFilterQuality`. Like Java's `Graphics2D` rendering hints, but as a value passed in.
