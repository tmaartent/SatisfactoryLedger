# Step 54 · Belts, pipes and lifts inspectable in 3D

**Goal:** in the 3D view, opened from the map or inside a factory's page, a click on a belt, pipe or lift shows what it is, as a click on a machine does (asked 2026-11-20). Branch: `feat/optimal-arrangement`, on top of steps 52 and 53 (uncommitted), uncommitted.

## What changed

- **A click on a belt, pipe or lift selects it.** It glows (a slightly thicker copy of its tube, in the selected building's yellow), and the panel shows:
  - its name, from the Catalog (*Conveyor Belt Mk.6*, *Conveyor Lift Mk.6*, *Pipeline Mk.2*);
  - **Bottleneck** in red, with *carries x of y, z more offered*, when the analysis found it a bottleneck (the map's tooltip, in words);
  - **On it**: the items on it when the game was saved.
  - A building and a belt aren't selected both at once. While defining a factory, a belt is selected but doesn't glow, the same as a building.
- **Lifts needed nothing extra.** The save reader already gives a lift a curve (its bottom and top), so the API sends lifts with the belts. They were drawn already; now they can be clicked too.
- **The height cut-off is respected.** A click goes through the part of a belt that the cut-off clips away, and finds what's drawn behind it.

## How: which belt did the click hit?

The 3D view merges belts into a few meshes, one per kind (belt, pipe) and per metre of height. Thousands of separate meshes made a big area slow (step 47). So a click hits a merged mesh, not a belt.

- Each merged mesh now keeps its belts in the order merged, and where each one's triangles start (`pieceStarts`).
- three.js's raycaster says which triangle was hit (`faceIndex`). `pieceAt` finds the belt it belongs to by binary search over those starts.
- Both are in `area/merged-pieces.ts`, test first (`merged-pieces.spec.ts`: seen failing, not compiling, then green).
- The tube is built in one place now (`tube(conveyor, at, extra)`): for drawing, for the factory preview (a bit thicker) and for the glow.

## Choices made (overrule if you like)

- **Only what the API already sends.** A belt's flow is known for bottlenecks only. The analysis computes every belt's flow but doesn't return it. Showing *carries x of y/min* for every belt would need the analysis response to include it: a small backend change. Say if you want it.
- **Bottlenecks aren't coloured red in 3D**, only named in the panel. The map colours them; the 3D view could too.

## Also changed: the map held to its picture

Asked 2026-11-20: zoomed out, the map showed the empty grid around the map picture.

- With map art, the map zooms out no further than the whole picture fits (`getBoundsZoom`). It can't be dragged past the picture's edges (`setMaxBounds`, with `maxBoundsViscosity: 1` so it stops dead, not elastic).
- The fit is worked out again when the map changes size (a wider window lets it zoom out a little further).
- Without map art, the map zooms out to the whole grid, as before.
- `mapArtExtent` in `map-model.ts` turns the picture's rectangle into the map's corners, test first. `holdToMapArt` in `map-page.ts` applies it.
- The faint grid lines stay over the picture.

## Files

`web/src/app/world/area/merged-pieces.ts` and spec (new); `area/area-page.ts` (selection of belts, `tube`, `drawSelectedConveyor`, the click), `area-page.html` (the panel, the hint), `area-page.scss` (bottleneck red).

193 web tests green, build clean. Not seen in a browser.

## Open: a stray error in the map's tests

`map-page.spec.ts` (from steps 52 and 53) sometimes reports one or two *unhandled errors* after its last test, though every test passes. The error: `Cannot read properties of undefined (reading 'clearRect')`. Leaflet's canvas redraws on an animation frame after the map was removed.

- Traced (with temporary logging in Leaflet, since removed): the frame was requested before `map.remove()`, and `remove()` cancels it with jsdom's own `cancelAnimationFrame`, using the right id. On its own, jsdom cancels correctly.
- Why it still fires isn't found yet. It looks like a test-environment issue, not one a browser would show. Not fixed.

## Web points

| Point | Java / TypeScript |
|---|---|
| **Merged geometry loses identity.** `mergeGeometries` concatenates index buffers in order, so triangle *n* of the result belongs to the piece whose range holds *n*. Keeping the ranges beside the mesh (`userData`) brings the identity back without separate meshes. | Like a batch insert that returns one count: you keep the offsets yourself to map rows back to inputs. |
| **A raycaster ignores clipping planes.** It hits geometry a `clippingPlanes` material doesn't draw, so the click filters hits by the plane itself (`point.y <= clip.constant`). | — |
