# Step 87 · Pick a factory by clicking it

**Goal:** the arrangement's Factories tab isn't needed: click the factories themselves on the map. Each click picks that factory alone, letting go of the one picked before, even when it was lit up as that one's supplier; clicking it again lets go (asked 2026-11-23). Branch `feat/ctrl-drag-arrangement`, uncommitted, after step 86.

## How it's built

- **`pickedAlone`** (`arrangement-model.ts`) replaces `toggled`: the factory clicked alone, or none when it's the one picked. The page's `toggleFocus` and the run's 3D belt floor (`belt-floor-3d-dialog.ts`, which shares the page's picking) both use it, so picking works alike before a run, on a run's plots and in 3D.
- **Before a run** a click on a factory's machine picks its factory (`drawWorld`, a Leaflet click on each machine's shape, stopped there so it isn't also a click on the map); not while drawing an area. With a run, a click on a factory's plot did already, now one at a time.
- **The Factories tab is gone**, with what only it used: `factoryList`, `machinePositions`, `moves`, `factoriesToArrange` and their tests, the list's styles, and `SidebarTab` on this page. Its one warning, links a run couldn't route, is above the map now.

Written into ADR-0027 and ADR-0031.

### Technical choices to overrule

- **A click on empty ground doesn't let go**, as it does on the world map: a drag with shift ends in a click there too.
- **Before a run, the factories aren't listed anywhere**: their names are in their tooltips.

## Tests

`pickedAlone`, 2 tests (the one clicked alone, even one lit up; letting go of the one picked); the page has no Factories tab. The tab's 3 page tests and `factoriesToArrange`'s 3 went, with `toggled`'s. Clicking a shape on a Leaflet canvas can't be done in jsdom, so the click itself isn't tested. 336 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
