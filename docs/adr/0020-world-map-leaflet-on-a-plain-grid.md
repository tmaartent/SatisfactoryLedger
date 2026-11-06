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
- **A plain grid behind it,** one line per kilometre, coordinates in metres. Nothing from the game's art.
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
  - The Catalog keeps a **Footprint** per class: the rectangle around its hard boxes, each box moved and turned by its offset. Soft boxes, the room kept free around a building, count only when a building has nothing else (a splitter).
  - Snapshots copy the footprints of the classes they have, like recipes.
  - The save reader keeps which way each buildable faces (its yaw), and the map turns the footprint by it.
  - A class without a footprint (a lift) is a dot.
- **Older snapshots** (version 4 and before) have no curves, yaws or footprints: re-import the save to see them. A catalog imported before footprints were read has none: re-import the Docs file first.
- Lifts have no curve and no footprint: they're dots.
- Leaflet 1.9 is CommonJS, so it's allowed in `angular.json` (`allowedCommonJsDependencies`).
