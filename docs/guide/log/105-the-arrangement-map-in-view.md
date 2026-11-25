# Step 105 · The arrangement's map in view, and Playwright tests for it

**Goal:** the owner, on an Android phone (2026-11-25), asked for more Playwright tests for the arrangement, and for four things to work:

1. Opening the page, the map should be visible; the Settings tab lay over it.
2. Picking a run from the list should centre the map on its area, as far as the map's edge allows.
3. Picking a factory in the Factories tab should close the tab and focus that factory.
4. While a run is shown, no area should be selectable: only after Reset, or with no run shown.

Branch `feat/arrangement-touch`, after step 104.

## What was wrong

1. **The open tab carried over.** The sidebar's state is shared by every page (`SidebarTabs`). Opening the world map's Arrangements tab (step 103) and tapping a run brought the arrangement page up with its sidebar open, on Settings. On a phone that's a sheet over the lower half of the map.
2. **The map fitted the area once per page** (`fitted`): a run picked later, or a run opened from its address before the runs had loaded, kept the view it had.
3. **A pick in the Factories tab only picked the factory.**
4. **Selecting didn't look at the run shown.**

## The owner's choice

- **The Factories tab closes only where it lies over the map** (phones, tablets, windows up to 960 px). On a wide desktop it sits beside the map and stays open for the next pick.

## What was built

- **The map in view on arriving:** the page's constructor calls `SidebarTabs.closeWhereItCovers()`, as the 3D view does (step 91).
- **The run's area, each run picked:**
  - `selectedRun` is the run shown once the runs are loaded. It compares by id (`computed(…, { equal })`), so asking for the runs again every 2 s while one computes changes nothing.
  - The effect that takes a run's area, exclusions and settings now runs on it. It clears `fitted`, so the map fits the new area.
  - `holdToExtent` still keeps the view inside the game's map, so near its edge the area is in view but not centred.
- **A factory picked in the Factories tab** (`pickFromList`):
  - picks it, as a click on the map does;
  - closes the tab where it covers;
  - centres the factory at the zoom the player chose, zooming out only as far as it takes to fit (`fitBounds` with `maxZoom` the current zoom).
  - It centres on the factory's plot in a run, or the rectangle around its machines before one. `AreaFactory.bounds` is new for this.
- **Selecting only for a new run** (`canSelect`: no run shown):
  - The select button is gone while a run is shown.
  - Shift and ctrl with a drag only move the map.
  - Selecting stops when a run comes up.
  - The hint above the map says *Reset to draw a new area*.
- **Docs:** ADR-0033, and the primer.

### Choices to overrule

- **"Focus" means centre, not zoom in.** A first version fitted the factory at up to 2 pixels a metre. On the test world's 120 m area that zoomed *out*, and on a large world it would jump far in.
- **A click on a factory on the map doesn't move the map,** only a pick in the tab does: the player is already looking there.

## Playwright tests (`e2e/arrangement.spec.ts`, *the optimal arrangement and its runs*)

The recording has no runs for the test world, so the tests answer them with `page.route`, as `e2e/test.ts` prescribes for what a test makes:

- two runs a short way apart, each with one factory on a plot;
- their machines, none.

The map draws on a canvas, with nothing in the page to measure. So `catchMaps` catches each Leaflet map as it's made: Leaflet 1.9 sets `window.L` as it loads, and an init script gives it an init hook. The tests then read what the map shows (`getBounds`), with no code in `src/` only for tests.

The four tests, on all four devices:

- **Opens on a run from the world map with its map in view,** though a tab was open there. It failed on both phones: the map was under the sheet.
- **Shows the area of the run picked from the list, in the middle of the map,** within 50 m. It failed on every device.
- **Centres a factory picked in the Factories tab,** its name within a tenth of the map's size from the middle, the tab closed where it covers. Set beside the page without its changes, it failed: the tab stayed open on the phone, and the factory stayed off to the side on desktop.
- **Offers no selecting while a run is shown, until Reset.** With touch: the select button. With a mouse: shift-drag shows no drawing hint, and after Reset it does. It failed on every device.

## Angular and Playwright points

- **`computed` with `equal`:** `computed(fn, { equal: (a, b) => a?.id === b?.id })` tells its readers of a change only when the id changes, though `fn` returns a new object each time the runs reload. Without it the effect would reset the picked factory every 2 s. Java has no counterpart in plain Java; RxJS has `distinctUntilChanged((a, b) => a.id === b.id)`.
- **`page.addInitScript`:** it runs before any of the page's own scripts. With `Object.defineProperty(window, 'L', { set })` the test sees Leaflet arrive and hooks it. Selenium has no equal short of a proxy.
- **`expect.poll` stops at an error:** a callback that throws, as reading a map not yet made did, fails the test at once rather than polling again. `shown` returns `null` until there's a map.

## Tests and results

- **Unit:** the model's factory rectangles (`AreaFactory.bounds`, 2 changed tests, failing first). The page's "stops selecting when a run is asked for" now also checks the select button is gone: its old version held on to a button that no longer existed.
- **Web:** 436 tests pass, exit code 0; lint, the format check, the build and the e2e type check pass.
- **Playwright:** 240 passed, 48 skipped, 1 failed in the full run. The failure was axe on the arrangement on a tablet, and it passed when the sweep ran again alone: flaky under load, as in step 103.
