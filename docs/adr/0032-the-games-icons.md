# ADR-0032: The game's icons, from the player's install

- **Status:** Accepted
- **Date:** 2026-11-23

## Context
Pages name items and buildings in words only: findings, the factory page, the 3D view's panel, the Catalog, the map's tooltips, the explanation's starts. Free resource nodes were dashed dots. The game has an icon for every item, resource and building, which a player recognises faster than a name. Like the map picture (ADR-0020), the icons are Coffee Stain's art.

## Decision
*(Decided 2026-11-23.)*
- **Where they come from: the map tool, from the player's own install**, as the map art (ADR-0015, ADR-0020). `--icons icons.json` reads the game's Docs file for each descriptor's big icon (`mPersistentBigIcon`), loads that texture from the game's files and writes it as PNG. 747 descriptors: items, resources, equipment and the descriptors buildings are built from.
  - The textures are DXT5 (631) or plain BGRA (110), 512 or 256 pixels square. DXT5 is decoded with `AssetRipper.TextureDecoder`, as DXT1 is for the map; SkiaSharp scales and writes PNG.
- **256 pixels square, PNG** *(asked)*: sharp at any size shown, transparent round the object. About 54 kB each, 56 MB as the tool's file (base64 inside JSON).
- **Where they live: imported into the Catalog**, in PostgreSQL *(asked)*, as an **icon set**: one at a time, an import replaces it whole, recorded as an import of its game version. Never committed: `icons.json` is in `.gitignore`, and tests use made-up images of a few bytes.
  - `POST /api/v1/catalog/icon-imports`. The file is larger than Kestrel's 30 MB default, so this endpoint takes up to 200 MB.
  - `GET /api/v1/catalog/icons`: which descriptors have one (404 before an import). `GET /api/v1/catalog/icons/{className}`: the PNG, cached for a day.
  - Only PNG gets in: an icon is served back as the type the file says.
- **A building shows its descriptor's icon.** Buildings have no icon of their own in the game; the descriptor they're built from has, named alike (`Build_ConstructorMk1_C`, `Desc_ConstructorMk1_C`). `IconSet.DescriptorOf` says which; 516 of the 546 buildables pair so, and the 30 that don't are decorations and the like.
- **Where they show** *(asked: all at once)*:
  - beside every item and building named in a page: the Catalog's recipes and nodes, the findings, the factory page's inputs and outputs, the 3D view's panel, the snapshot's unsupported buildings, the explanation's starts;
  - in the map's tooltips: a machine, belt, provider, building, a generator's fuel, a node's resource;
  - **every resource node** on the map, free or with a miner or extractor on it, is its resource's icon *(asked; every node, not only the free ones, asked 2026-11-23)*, where a free node was a dashed dot. Its tooltip says what stands on it, or that it's free. It's a pin, as on the game's map: the icon on its purity's colour (green pure, yellow normal, red impure) in a white ring, up and to the left, on a white stick down to a dot where the node lies. The map's other purity marks, the edges of miners on nodes, take the same colours. It grows as the map is zoomed in, half as fast as the map, from 18 to 72 pixels *(both asked 2026-11-23)*.
  - Not inside sentences (*Not enough Iron Ingot*), nor in the arrangement's tooltips.
- **Without icons** (before an import) everything shows as before: names, and the nodes as dots.

## Alternatives considered
- **A local folder the API serves:** simpler, but outside the database and its backups, and each machine needs the folder. Not taken, as for the map art.
- **64-pixel icons:** a quarter of the size, sharp enough at 20 pixels; you chose 256.
- **A community site's icons:** someone else's copy of the same art, with its own terms, and not matched to the player's game version.

## Consequences
- **One more file to import after a game update,** next to the Docs file, the node table and the map art.
- **A page with many names loads many icons** (50–130 kB each) the first time; the browser keeps them a day.
- **The web app knows the building-to-descriptor rule too** (`CatalogNames.icon`), to show no broken image for a class without one: it's the same one line as `IconSet.DescriptorOf`.
