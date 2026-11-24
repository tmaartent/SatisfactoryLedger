# Step 92 · A tap says what a building is

**Goal:** on a touch screen, where nothing hovers, a tap on a building says what it is, as its tooltip does on hover (ADR-0033, asked 2026-11-24). Branch `feat/tap-labels`, on top of step 91 (`feat/select-mode`, PR #28).

## What was already there

Measured at phone size with real touch events before building anything: **most of it worked.** Leaflet opens a non-permanent tooltip on `mouseover` *and* on `click` (`Tooltip.js`, `_initTooltipInteractions`), and a tap fires a click. So a tap on a machine of no factory, a belt, a pipe or a resource node already showed its label; on a machine of no factory it also opens the explanation, as a click does. A tap on empty map closed it. In 3D, a tap on a building selects it and opens the Info tab.

**The gap:** a factory's building. A click on one opens the factory's page at once (`opensFactory`); with a mouse the pointer has shown its label on the way there, but on a touch screen the tap left the map before anything could be read.

## What was built

The owner chose of three (leave it; first tap shows, second opens; a label with a link): **the first tap shows, a second opens.**
- On a touch screen, the first tap on a factory's building opens its label and highlights its factory, as picking it in the Factories tab does: the rest fades and the map zooms to it.
- A second tap on the same building, while its factory is still the one highlighted, opens the factory's page.
- **A tap on the label opens it too** (asked 2026-11-24, after trying it: "tap again to open" invites a tap on the label, which did nothing).
- Its label says "Factory **Rubber** · tap again to open"; with a mouse, "click to open", as before.
- A tap on empty map lets go of it, as of a highlight.
- With a mouse nothing changes: a click opens the page at once.

## How

- `world/map/map-page.ts`:
  - `tapped`, a signal: the building tapped once, by id.
  - `opensFactory(shape, id, owner)`: on a touch screen, unless this is the second tap on the building of the factory highlighted, it highlights the factory, keeps the id and opens the tooltip; else it opens the page.
  - `reopenTapped`: highlighting draws every machine anew, with new shapes, so the label opened on the old shape would be gone. Each drawing reopens it on the new shape of the building tapped. It reads `tapped` `untracked`, so a tap doesn't redraw the map by itself.
  - `openLabel`: opens the tooltip and makes it tappable. Leaflet's tooltips take no pointer (`pointer-events: none` in its stylesheet), so this one gets `pointer-events: auto` and an `onclick` that opens the factory; `stopPropagation` keeps the tap from the map, whose click lets go of the highlight. A property, not `addEventListener`, so opening the label again doesn't add a second handler.
  - `touchScreen()`: `matchMedia('(hover: none)')`, the question the stylesheets ask.

### Technical choices to overrule

- **"The same building"**, not any building of the factory, takes the second tap. A tap on another of its buildings shows that one's label first.
- **The first tap zooms to the factory**, as highlighting does everywhere. The building tapped stays in view, as part of it.

## Tests

- `world/map/map-page.spec.ts`, *a factory's building tapped on a touch screen*: the first tap highlights the factory, opens the label with "tap again to open" and stays on the map; a second tap opens the page, and so does a tap on the label; with a mouse, the first click does.
  - jsdom can't hit-test a canvas, so the tests fire Leaflet's `click` on the building's shape, as the canvas renderer does once it finds the shape under the finger.
  - `matchMedia` is stubbed with `addListener` and `removeListener` too: Material's `BreakpointObserver` calls them, and without them Vitest counted unhandled errors and the run exited with 1 although every test passed.
- 375 web tests pass (exit code 0); lint and the format check pass.
- With real touch events over the DevTools protocol at 390 × 844: the first tap on a Blender showed its label and highlighted its factory; the second, where the zoom had moved it, opened the factory's page; so did a tap on the label.
