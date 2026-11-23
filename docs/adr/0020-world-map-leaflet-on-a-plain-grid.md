# ADR-0020: The world map draws a snapshot with Leaflet, on a plain grid

- **Status:** Accepted
- **Date:** 2026-11-06

## Context
Every finding has a position, but tables don't show where things are, or how far apart. The second draft of the web app (ADR-0019) is a map of a snapshot.

## Decision
- **What it shows** (decided 2026-11-06):
  - machines, coloured by how they run (BR-A11);
  - belts and pipes as built, bottlenecks red (BR-A12; added the same day, after you asked whether belts keep their shape);
  - providers, ringed by the purity of their node;
  - splitters, mergers, lifts, storage and consumers, so belts don't end in mid-air;
  - every building as the ground it covers, turned the way it faces (its **footprint**), not as a dot;
  - the resource nodes no provider stands on;
  - one factory highlighted, picked from the traced factories.
- **A plain grid behind it,** one line per kilometre, coordinates in metres. Nothing from the game's art (*amended 2026-11-20:* the game's map picture lies under it, from your own install; see the end).
- **A map endpoint:** `GET /api/v1/world/snapshots/{id}/map` returns the snapshot's machines and providers with their positions, and each provider's resource, node and purity. It also returns the belts and pipes with their curves, as the game stores them.
  - How a machine runs isn't in it: the page takes that from the findings. So the map shows the machines while the analysis is pending, grey until it's ready.
- **Leaflet 1.9** draws it with its flat coordinate system (`CRS.Simple`), on a canvas for thousands of markers.
  - One map unit is one metre.
  - North is up: the game's y grows southwards, so the latitude is −y.

## Alternatives considered
- **The game's map as background:** the game's art, and an extraction step. A local-only image could come later.
- **Only the findings' positions:** no API change, but no providers or stations.
- **Plain SVG or canvas without a library:** zooming, panning and tooltips would be ours to write.
- **OpenLayers:** more capable, but heavier, and geographic projections aren't needed here.
- **Leaflet 2.0:** still in beta.

## Consequences
- The map endpoint is in the public contract (`api/openapi.json`).
- **Belts and pipes keep their shape.** The save has each one's curve (`mSplineData`): points relative to the belt, each with an arrive and a leave tangent. Between two points it's a cubic Hermite spline. The build mode (straight, curved…) isn't saved, only the resulting curve.
  - The save reader turns the curve into world coordinates (the belt's position and rotation).
  - Snapshots keep it rounded to 0.01 cm (snapshot version 5).
  - The web app samples it into a line: straight stretches as their two ends, bends in 8 steps.
- **Footprints come from the Docs file** (decided 2026-11-06). Each building class has clearance boxes there (`mClearanceData`), in its own coordinates.
  - The Catalog keeps a **Footprint** per class: the rectangle around its hard boxes, each box moved and turned by its offset. The turn is applied in 3D, all eight corners, before the rectangle is taken seen from above (fixed 2026-11-18: only the turn around the vertical was applied, so the Particle Accelerator's tilted arch counted flat, and boxes tilted a quarter or more came out mirrored; it covered 50 metres instead of 37). The map draws a building as **each of its boxes seen from above** (decided 2026-11-18), one Leaflet polygon of several outlines with a non-zero fill so overlapping boxes don't cut holes: a Quantum Encoder is its cross of boxes, not the rectangle around them. The rectangle stays for what needs one size. Soft boxes, the room kept free around a building, count only when a building has nothing else (a splitter).
  - Snapshots copy the footprints of the classes they have, like recipes.
  - The save reader keeps which way each buildable faces (its yaw), and the map turns the footprint by it.
  - A class without a footprint (a lift) is a dot.
- **Older snapshots** (version 4 and before) have no curves, yaws or footprints: re-import the save to see them. A catalog imported before footprints were read has none: re-import the Docs file first.
- Lifts have no curve and no footprint: they're dots.
- **Contents** (decided 2026-11-06, step 22): what's on belts, lifts, splitters and mergers, and in machines, at save time.
  - Kept apart from stock, so it never counts as supply in the analysis.
  - The map shows it in tooltips, and colours belts by their main item on request.
  - Snapshot version 6; older snapshots have none.
- Leaflet 1.9 is CommonJS, so it's allowed in `angular.json` (`allowedCommonJsDependencies`).

*Amended 2026-11-20 ([log 53](../guide/log/53-the-games-map-under-the-map.md)):* **the game's own map picture lies under the map** (asked 2026-11-20), the "local-only image" this ADR left for later.
- **From your own install, never from the internet or the repository.** The map tool reads it (ADR-0015, amended the same day): the textures `Map_column-row` (2 by 2, 4096 pixels each, DXT1) and where the game's capture actor saw them from, so the placement is the game's, not typed in. In 1.2 that's a square 750,000 cm wide around (50,301.832, 0). Checked by drawing all 577 resource nodes on it: every one lands on land.
- **Imported into the Catalog** (decided 2026-11-20), as the resource nodes are: `POST /api/v1/catalog/map-art-imports`, stored in PostgreSQL as JPEG (about 10 MB), one at a time, an import replacing the one before. `GET /api/v1/catalog/map-art` says where each tile lies; `GET /api/v1/catalog/map-art/tiles/{column}/{row}` serves it, cached for a day. Only JPEG and PNG are taken in, since a tile is served back as the type the file says.
- **The map draws the tiles first**, so everything else lies over them, with or without a snapshot. Without map art (404) it's the plain grid, as before. The grid stays over the picture.
- **The map is held to the picture:** it zooms out no further than the whole picture and moves no further than its edges. *Amended 2026-11-23 (step 66):* where a snapshot's buildings, belts or pipes stand past the picture, as the game allows, those edges reach to the furthest of them and 50 m past. The grid shows there.
- **The picture is Coffee Stain's art.** `map-art.json` is in `.gitignore`; tests use made-up tiles of a few bytes.
- **Alternatives:** exporting it by hand with FModel (a manual step per game update, and the placement calibrated by hand), or a community site's tiles (someone else's server, terms and updates, online only).

## Amendment 2026-11-23: under the optimal arrangement too
The optimal arrangement's map has the map picture under it as well (asked by the owner, step 84): the same tiles, from the same `GET /catalog/map-art`, laid first so everything else lies over them. Both maps use one projection (`toMapPoint`), so the tiles land where they do on the world map. Since step 85 it's held to the picture's edges as the world map is. Both load the picture from one service (`MapArt`) and hold their map with one function (`holdToExtent`), so they can't drift apart.

