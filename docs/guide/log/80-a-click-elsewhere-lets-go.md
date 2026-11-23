# Step 80 · A click elsewhere lets go

**Goal:** a factory picked on the map (in the Factories tab, or coming back from its page) fades the rest, and so does a count clicked in the Info tab; only picking it again undid it. Now a click anywhere else on the map lets go of it, and nothing is faded (asked 2026-11-23). Branch `feat/node-filter`, uncommitted, after step 79 (PR #22 is open).

## How it's built

- **One handler on the map** (`MapPage.createMap`): Leaflet's `click` on the map sets the highlighted factory and the count shown to none, which the map's `highlighted` set follows; the fading, rings and outlines go with it.
- **What stops it:** a shape that does something when clicked, a factory's machine (opens the factory) or a held-back machine (explains it), already stops its click from reaching the map (`L.DomEvent.stopPropagation`). Other buildings, belts and the background count as *elsewhere*. While drawing an area for 3D the map leaves the highlight be.
- The `?factory=` in the URL, from coming back from a factory's page, stays: the factory is picked from it once, as before, and a click lets go of it.

Written into ADR-0031.

### Technical choices to overrule

- **A click on another building lets go too,** not only on empty ground: it's not the factory or count picked.
- **The URL keeps `?factory=`** after the click: reloading the page highlights that factory again.

## Tests

2 new on the map page: a factory picked, then a click on the map, and none is highlighted; a count shown, then a click on the map, and it's no longer shown. 320 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
