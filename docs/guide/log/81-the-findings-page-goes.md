# Step 81 · The findings page goes, and shift-drag opens 3D

**Goal:** everything the owner wants from the findings page is on the map now, so the page goes. And opening an area in 3D needn't be a button: hold shift and drag, as defining a factory in 3D does, with a line in the header saying so (asked 2026-11-23). The optimal arrangement stays, as the owner said while this was being built; where it's opened from comes next. Branch `feat/node-filter`, uncommitted, after step 80 (PR #22 is open).

## How it's built

- **The findings page is gone:** `SnapshotPage` (`snapshot-page.*`), its table of findings `FindingsView` (`findings.*`), and `AnalysisStatusChip`, which only it used. `/snapshots/{id}` redirects to the snapshot's map (`app.routes.ts`), so a link or bookmark to it still lands somewhere. The map's header loses its *Findings* link.
  - **Carried over to the map's Info tab**, under what the snapshot holds, when there are any: *Recipes the catalog doesn't know* (import a newer Docs file, then the save) and *Buildings the analysis leaves out*. My default: the owner didn't pick when asked.
  - **Gone with it:** the lists of generators (fuel, why below full power) and drone ports (what their drones burn, what reaches them, how much is short), and the machines table with its filters. The map has the counts, its tooltips and the Info tab's clickable counts.
  - **Elsewhere already:** its save date and delete in the snapshot picker (step 79), its analysis state in the Info tab.
  - **The optimal arrangement** (`/snapshots/{id}/arrangement`) stays as it is; only the findings page linked to it, so for now it's reached by its address. Its back button points at `/snapshots/{id}`, which now leads to the map.
- **Shift and drag** (`MapPage.listenForArea`): the map's container listens for `pointerdown` in the capture phase; with shift held, it starts drawing and switches Leaflet's panning off before Leaflet's own handler starts it. The rectangle is drawn on Leaflet's mouse events as before; letting go opens it in 3D. Any `pointerup` ends drawing, so panning comes back even after a press on a resource node's pin, which never reaches the map's `mousedown`. Leaflet's own shift-drag, zooming to a box, is off (`boxZoom: false`). The button and `toggleDrawing` are gone; the header says *Hold shift and drag to see an area in 3D*, and while drawing, *Let go to see this area in 3D*.

Written into ADR-0031 and ADR-0024.

### Technical choices to overrule

- **The import warnings moved to the Info tab**; the generator and drone port lists didn't.
- **The old address redirects** rather than showing *not found*.

## Tests

3 new on the map page: the header's hint with no button and no Findings link; a shift-drag opens that area in 3D; the Info tab's import warnings. The findings page's tests (12) went with it. 311 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
