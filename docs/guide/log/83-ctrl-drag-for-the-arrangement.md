# Step 83 · Ctrl and drag for the optimal arrangement

**Goal:** the optimal arrangement lost its only way in when the findings page went (step 81). Now a drag with ctrl held on the world map opens it, with the area dragged over ready to arrange, as a drag with shift held opens it in 3D (asked 2026-11-23). Branch `feat/ctrl-drag-arrangement`, from `main` after #23.

## How it's built

- **The world map** (`MapPage.listenForArea`): the press is caught in the capture phase as for shift; with ctrl (or ⌘ on a Mac) held, the drag is an arrangement's. `drawing` says which (`'3d' | 'arrangement' | null`), so the hint under the cursor says what letting go does. The drag keeps which it was from its start: the `pointerup` safety net ends drawing before Leaflet's `mouseup` comes. Letting go opens `/snapshots/{id}/arrangement?x0=…&y0=…&x1=…&y1=…`. The header says *Hold shift and drag to see an area in 3D, or ctrl to arrange its factories*.
- **The arrangement page** takes the area from the address: `areaFromQuery` (`arrangement-model.ts`) reads the four corners, or none when one is missing or isn't a number. Its `area` is now a `linkedSignal` of that, so drawing another area or picking a run still replaces it. Given an area, no earlier run is picked by default: a run's area would replace the one dragged.

Written into ADR-0027, ADR-0024 and ADR-0031.

### Technical choices to overrule

- **⌘ counts as ctrl on a Mac**, where ctrl and a click opens the context menu.
- **The page's own settings** (plot size, roads, solver time) start at its defaults, not those of the last run.
- **The page doesn't zoom to the area dragged**; it shows it drawn on its map as before.

## Tests

3 new: the area from the address, and none for a missing or wrong corner (`arrangement-model.spec.ts`); a ctrl-drag on the world map opens the arrangement with the area. The header test checks both hints. `ArrangementPage` has no spec (Leaflet). 315 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
